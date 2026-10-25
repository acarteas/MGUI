using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

internal readonly record struct WorkloadState(int SimulationFrame, int WorldOffset, string Score, float ScrollOffset);

internal sealed partial class BenchmarkFixture : IRenderHost, IDisposable
{
    private readonly Game Game;
    private readonly Options Options;
    private readonly MainRenderer Renderer;
    private readonly FontSystem Font;
    private readonly Texture2D NineSlice;
    private readonly Effect LayerEffect;
    private readonly MGTextBlock Score;
    private MGScrollViewer? Scroller;
    public MGDesktop Desktop { get; }
    public WorkloadState State { get; private set; }
    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;

    public BenchmarkFixture(Game game, Options options)
    {
        Game = game;
        Options = options;
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        Font = new();
        Font.AddFont(fontBytes);
        FontStashSharpTextEngine textEngine = new();
        textEngine.AddFontSystem("NotoSans", CustomFontStyles.Normal, Font, fontBytes);
        Renderer = new(this, new FontManager("NotoSans")) { TextEngine = textEngine };
        Desktop = new(Renderer);
        Desktop.SetUniformUIScale(options.Scale);
        NineSlice = new(GraphicsDevice, 12, 12);
        Color[] texels = new Color[144];
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                texels[y * 12 + x] = x < 3 || x >= 9 || y < 3 || y >= 9 ? new Color(70, 160, 190, 210) : new Color(15, 42, 64, 170);
            }
        }

        NineSlice.SetData(texels);
        LayerEffect = game.Content.Load<Effect>("EffectParameters").Clone();
        MGWindow window = new(Desktop, 48, 48, Math.Min(options.Width - 96, (int)(1000 * options.Scale)), Math.Min(options.Height - 144, (int)(540 * options.Scale)))
        {
            WindowStyle = WindowStyle.None,
            Padding = new(12),
            BackgroundBrush = new(new MGSolidFillBrush(new Color(12, 23, 38) * 0.72f)),
            ClipToBounds = true
        };
        Desktop.Windows.Add(window);
        Score = Text(window, "SCORE 001200", Color.Gold, 20);
        if (options.IsComparison)
        {
            BuildComparison(window);
            return;
        }
        MGStackPanel root = Stack(window, Orientation.Vertical, 8);
        if (options.Mode == "disabled")
        {
            MGRenderCache disabled = new(window) { IsCacheEnabled = false, Padding = new(0) };
            disabled.SetContent(root);
            window.SetContent(disabled);
        }
        else
        {
            window.SetContent(root);
        }
        if (options.Scenario is "hud" or "animated-score")
        {
            BuildHud(window, root);
        }
        else if (options.Scenario is "scrolling" or "nested-clips")
        {
            BuildScroll(window, root, options.Scenario == "nested-clips");
        }
        else
        {
            BuildBatched(window, root, options.Scenario == "layered");
        }
    }

    private static MGStackPanel Stack(MGWindow window, Orientation orientation, int spacing) => new(window, orientation) { Spacing = spacing, Padding = new(3) };
    private static MGTextBlock Text(MGWindow window, string text, Color color, int size = 14) => new(window, text, color, size, false) { WrapText = false };

    private void BuildHud(MGWindow window, MGStackPanel root)
    {
        MGStackPanel top = Stack(window, Orientation.Horizontal, 24);
        top.TryAddChild(Text(window, "NORTH  /  ROUND 07", Color.White, 20));
        top.TryAddChild(Score);
        top.TryAddChild(Text(window, "RIVAL 000975", Color.Coral, 20));
        root.TryAddChild(top);
        MGStackPanel bottom = Stack(window, Orientation.Horizontal, 18);
        foreach (string label in new[] { "HEALTH 85 / 100", "ENERGY 6 / 10", "AMMO 12", "TURN 03", "READY" })
        {
            bottom.TryAddChild(Text(window, label, Color.LightCyan));
        }

        root.TryAddChild(bottom);
    }

    private void BuildBatched(MGWindow window, MGStackPanel root, bool layered)
    {
        Random random = new(Options.Seed);
        for (int y = 0; y < 8; y++)
        {
            MGStackPanel row = Stack(window, Orientation.Horizontal, 8);
            for (int x = 0; x < 6; x++)
            {
                MGTextBlock cell = Text(window, $"ITEM {y * 6 + x:00}", Color.White);
                cell.PreferredWidth = 140;
                cell.PreferredHeight = 42;
                cell.Padding = new(8);
                IFillBrush fill = new MGSolidFillBrush(new Color(random.Next(25, 70), random.Next(65, 110), random.Next(100, 150)) * 0.65f);
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

                cell.BackgroundBrush = new(fill);
                row.TryAddChild(cell);
            }

            root.TryAddChild(row);
        }
    }

    private void BuildScroll(MGWindow window, MGStackPanel root, bool nested)
    {
        root.TryAddChild(Text(window, nested ? "NESTED CLIPPED PANELS" : "SCROLLING LIST", Color.LightCyan, 20));
        Scroller = new(window) { PreferredHeight = 390, ClipToBounds = true };
        MGStackPanel list = Stack(window, Orientation.Vertical, 5);
        Scroller.SetContent(list);
        root.TryAddChild(Scroller);
        for (int row = 0; row < 60; row++)
        {
            if (nested)
            {
                MGScrollViewer inner = new(window, ScrollBarVisibility.Disabled, ScrollBarVisibility.Hidden)
                {
                    PreferredHeight = 58,
                    PreferredWidth = 820,
                    ClipToBounds = true,
                    Padding = new(6),
                    BackgroundBrush = new(new MGSolidFillBrush(new Color(30, 90, 120) * 0.60f))
                };
                MGStackPanel contents = Stack(window, Orientation.Horizontal, 20);
                for (int column = 0; column < 10; column++)
                {
                    contents.TryAddChild(Text(window, $"CLIP {row:00}:{column:00} overflow", Color.White));
                }

                inner.SetContent(contents);
                list.TryAddChild(inner);
            }
            else
            {
                MGTextBlock item = Text(window, $"ENTRY {row:00}   steady content with a moving scroll viewport", row % 2 == 0 ? Color.White : Color.LightSkyBlue);
                item.PreferredHeight = 34;
                list.TryAddChild(item);
            }
        }
    }

    public void Update(int frame)
    {
        PreviewUpdate?.Invoke(this, TimeSpan.FromSeconds((frame + 1) / 60.0));
        if (Options.Scenario is "animated-score" or "compare-mixed" or "compare-dirty")
        {
            Score.Text = $"SCORE {1200 + frame:000000}";
        }

        if (Scroller != null)
        {
            Scroller.VerticalOffset = frame * 2 % Math.Max(1, (int)Scroller.MaxVerticalOffset);
        }

        UpdateComparison(frame);
        Desktop.Update();
        EndUpdate?.Invoke(this, EventArgs.Empty);
        State = new(frame, frame % 64, Score.Text, Scroller?.VerticalOffset ?? 0);
    }

    internal CacheCounters GetCacheCounters()
    {
        MGRenderCacheManager manager = Desktop.RenderCaches;
        var statistics = manager.Statistics;
        return new(statistics.Allocations, statistics.Builds, statistics.TargetSwitches,
            manager.RetainedBytes, manager.EntryCount, manager.PendingCount, statistics.Hits, statistics.Misses, statistics.Evictions, statistics.Disposals);
    }

    public Rectangle GetBounds() => new(0, 0, Options.Width, Options.Height);
    public MouseState GetMouseState() => default;
    public KeyboardState GetKeyboardState() => default;
    public object? GetService(Type serviceType) => Game.Services.GetService(serviceType);

    public void Dispose()
    {
        Desktop.DisposeRenderCaches();
        LayerEffect.Dispose();
        NineSlice.Dispose();
        Renderer.SpriteBatch.Dispose();
        Renderer.PrimitiveBatch.Dispose();
        Renderer.Content.Dispose();
        Font.Dispose();
    }
}
