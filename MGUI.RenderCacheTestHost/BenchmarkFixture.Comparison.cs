using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace MGUI.RenderCacheTestHost;

internal readonly record struct ComparisonState(string FirstCellText, int Left, int Top, int Width, int Height);

internal sealed partial class BenchmarkFixture
{
    private readonly List<MGTextBlock> ComparisonCells = new();
    private readonly List<MGStackPanel> ComparisonRows = new();

    private void BuildComparison(MGWindow window)
    {
        MGStackPanel outer = Stack(window, Orientation.Vertical, 8);
        window.SetContent(outer);
        MGStackPanel art = Stack(window, Orientation.Vertical, 8);
        art.BackgroundBrush = new(new MGSolidFillBrush(new Color(12, 23, 38)));
        bool layered = Options.Scenario != "compare-cells";
        Random random = new(Options.Seed);
        for (int y = 0; y < 6; y++)
        {
            MGStackPanel row = Stack(window, Orientation.Horizontal, 8);
            for (int x = 0; x < 6; x++)
            {
                MGTextBlock cell = Text(window, $"ITEM {y * 6 + x:00}", Color.White);
                cell.PreferredWidth = 140;
                cell.PreferredHeight = 42;
                cell.Padding = new(8);
                IFillBrush fill = new MGSolidFillBrush(new Color(random.Next(25, 70), random.Next(65, 110), random.Next(100, 150)));
                if (layered)
                {
                    MGNineSliceFillBrush nineSlice = new(new(6), new MGTextureData(NineSlice), new(3));
                    MGEffectFillBrush effect = new(LayerEffect)
                    {
                        UseStandardParameters = true,
                        Parameters = new[]
                        {
                            new MGEffectParameterValue("CustomInt", MGEffectParameterType.Int, 102),
                            new MGEffectParameterValue("CustomColor", MGEffectParameterType.Color, (new Color(20, 70, 100) * 0.22f).ToVector4())
                        }
                    };
                    fill = new MGCompositedFillBrush(fill, nineSlice, effect);
                }

                // Mode 102 returns constant premultiplied CustomColor * vertex color. It does not use
                // time, screen position or interaction; all textures and parameters stay fixed here.
                // A disabled assertion wrapper lets an enclosing panel verify the same source contract.
                if (Options.Mode is "background" or "panel")
                {
                    fill = new MGCachedFillBrush(fill)
                    {
                        SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic,
                        IsCacheEnabled = Options.Mode == "background"
                    };
                }
                cell.BackgroundBrush = new(fill);
                ComparisonCells.Add(cell);
                row.TryAddChild(cell);
            }
            ComparisonRows.Add(row);
            art.TryAddChild(row);
        }

        MGElement content = art;
        if (Options.Scenario == "compare-scroll")
        {
            Scroller = new(window) { PreferredHeight = 200, ClipToBounds = true };
            Scroller.SetContent(art);
            content = Scroller;
        }
        if (Options.Mode == "panel")
        {
            MGRenderCache boundary = new(window) { Padding = new(0), ClipToBounds = true };
            boundary.SetContent(content);
            content = boundary;
        }
        outer.TryAddChild(content);

        // This is an ordinary sibling outside the explicit boundary, updated at the same cadence.
        outer.TryAddChild(Score);
    }

    internal ComparisonState? GetComparisonState()
    {
        if (ComparisonCells.Count == 0)
        {
            return null;
        }
        var cell = ComparisonCells[0];
        var bounds = cell.LayoutBounds;
        return new(cell.Text, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void UpdateComparison(int frame)
    {
        if (Options.Scenario == "compare-churn")
        {
            int transition = Options.Warmup + Options.Frames / 2;
            if (frame == transition - 2)
            {
                ComparisonCells[0].PreferredWidth = 156;
            }
            if (frame == transition)
            {
                ComparisonRows[0].TryRemoveChild(ComparisonCells[0]);
                ComparisonRows[1].TryRemoveChild(ComparisonCells[6]);
                ComparisonRows[0].TryAddChild(ComparisonCells[6]);
                ComparisonRows[1].TryAddChild(ComparisonCells[0]);
            }
        }
        if (Options.Scenario == "compare-change" && frame == Options.Warmup + Options.Frames / 2)
        {
            ComparisonCells[0].Text = "CHANGED";
            Score.Text = "SCORE 001201";
        }
        if (Options.Scenario == "compare-dirty")
        {
            ComparisonCells[0].Text = $"ITEM {frame % 100:00}";
        }
    }
}
