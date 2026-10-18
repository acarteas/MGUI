using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

// The final matrix reuses the full native input/pulse fixture, rather than approximating its state with setters.
internal sealed partial class LiveSiblingTests
{
    internal static int RunMatrix(int width, int height, float scale)
    {
        if ((width, height) is not ((1280, 720) or (1920, 1080)) || scale is not (1f or 1.5f or 2f))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Matrix requires 1280x720 or 1920x1080 at layout scale 1, 1.5 or 2.");
        }
        using LiveSiblingTests game = new(width, height, scale);
        game.Run();
        Check(game.Completed, "Final matrix cell did not complete.");
        Console.WriteLine("RESULT:" + System.Text.Json.JsonSerializer.Serialize(game.Checks));
        Console.WriteLine("ARTIFACTS:" + game.Evidence);
        return 0;
    }

    private void WriteMatrixPng(string path, Color[] pixels)
    {
        using Texture2D image = new(GraphicsDevice, SurfaceWidth, SurfaceHeight);
        image.SetData(pixels);
        using FileStream output = File.Create(path);
        image.SaveAsPng(output, SurfaceWidth, SurfaceHeight);
    }

    private void TestMatrixMutations(float scale)
    {
        using Texture2D first = new(GraphicsDevice, 8, 8);
        using Texture2D second = new(GraphicsDevice, 8, 8);
        first.SetData(Enumerable.Repeat(Color.OrangeRed, 64).ToArray());
        second.SetData(Enumerable.Range(0, 64).Select(index => index % 2 == 0 ? Color.Lime : Color.Blue).ToArray());
        using Pair pair = new(this, scale, 1);
        pair.Warm();
        string prefix = "mutation-s" + ScaleName(scale);
        (MGStackPanel Panel, MGTextBlock Text, MGImage Image) Create(LiveSiblingFixture fixture)
        {
            MGStackPanel panel = new(fixture.Window, Orientation.Vertical)
            {
                Spacing = 3, Padding = new(6), BackgroundBrush = new(new MGSolidFillBrush(Color.DarkSlateBlue))
            };
            MGTextBlock text = new(fixture.Window, "STATIC glyphs gjpq", Color.White, 14) { WrapText = false };
            MGImage image = new(fixture.Window, new MGTextureData(first)) { PreferredWidth = 48, PreferredHeight = 32 };
            panel.TryAddChild(text);
            panel.TryAddChild(image);
            panel.TryAddChild(new MGRectangle(fixture.Window, 80, 25, Color.Gold, 2, Color.Red * 0.5f));
            fixture.StaticArt.SetContent(panel);
            return (panel, text, image);
        }
        var cached = Create(pair.Cached);
        var direct = Create(pair.Direct);
        Point neutral = new(15, 15);
        int captures = pair.Cached.StaticArt.Captures;
        pair.Step(prefix + "-content-first-miss", neutral, save: true);
        Check(pair.Cached.StaticArt.Captures == captures, "Content replacement captured without direct observation.");
        pair.Step(prefix + "-content-rebuilt", neutral);
        Check(pair.Cached.StaticArt.Captures == captures + 1, "Changed content did not rebuild.");
        Checks.Add(prefix + "-first-miss-content-and-layered-alpha-exact", true);

        string Region(Color[] pixels)
        {
            Rectangle bounds = pair.Cached.StaticBoundary.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
                pair.Cached.StaticBoundary.LayoutBounds);
            Check(new Rectangle(0, 0, SurfaceWidth, SurfaceHeight).Contains(bounds), "Mutation boundary is not fully visible.");
            return PixelHash(Enumerable.Range(bounds.Top, bounds.Height)
                .SelectMany(y => pixels.Skip(y * SurfaceWidth + bounds.Left).Take(bounds.Width)).ToArray());
        }
        Color[] prior = pair.Step(prefix + "-baseline", neutral);
        cached.Text.Text = direct.Text.Text = "CHANGED letters Wxyz";
        Color[] changed = pair.Step(prefix + "-text-direct", neutral);
        Check(Region(prior) != Region(changed), "Text mutation omission control did not change UI pixels.");
        pair.Step(prefix + "-text-rebuilt", neutral);
        cached.Text.FontSize = direct.Text.FontSize = 18;
        changed = pair.Step(prefix + "-font-direct", neutral);
        pair.Step(prefix + "-font-rebuilt", neutral);
        cached.Text.Foreground = direct.Text.Foreground = new(Color.Gold);
        pair.Step(prefix + "-foreground-direct", neutral);
        pair.Step(prefix + "-foreground-rebuilt", neutral);
        cached.Image.Source = direct.Image.Source = new MGTextureData(second);
        prior = pair.Step(prefix + "-image-direct", neutral);
        pair.Step(prefix + "-image-rebuilt", neutral);
        cached.Image.Source = direct.Image.Source = new MGTextureData(first);
        changed = pair.Step(prefix + "-image-omission-control", neutral);
        Check(Region(prior) != Region(changed), "Image replacement omission control did not change UI pixels.");
        pair.Step(prefix + "-image-restored", neutral);
        Checks.Add(prefix + "-text-font-foreground-image-mutations", true);

        captures = pair.Cached.StaticArt.Captures;
        int paints = pair.Cached.StaticArt.Paints;
        pair.Step(prefix + "-omitted-prepare", neutral, prepare: false);
        Check(pair.Cached.StaticArt.Captures == captures && pair.Cached.StaticArt.Paints == paints + 1,
            "Omitted Prepare reused a ready target.");
        pair.Step(prefix + "-prepare-restored", neutral);
        captures = pair.Cached.StaticArt.Captures;
        pair.Step(prefix + "-late-after-prepare", neutral, afterPrepare: fixture =>
        {
            if (ReferenceEquals(fixture, pair.Cached)) { cached.Text.Foreground = new(Color.Cyan); }
            else { direct.Text.Foreground = new(Color.Cyan); }
        });
        Check(pair.Cached.StaticArt.Captures == captures, "Late mutation captured before observation.");
        pair.Step(prefix + "-late-rebuilt", neutral);
        Check(pair.Cached.StaticArt.Captures == captures + 1, "Late mutation did not rebuild its observed generation.");
        captures = pair.Cached.StaticArt.Captures;
        pair.Step(prefix + "-late-layout-direct", neutral, afterPrepare: fixture =>
        {
            if (ReferenceEquals(fixture, pair.Cached)) { cached.Text.Text = "LATE layout text"; }
            else { direct.Text.Text = "LATE layout text"; }
        });
        Check(pair.Cached.StaticArt.Captures == captures, "Late layout mutation captured before observation.");
        pair.Step(prefix + "-late-layout-settled-direct", neutral);
        pair.Step(prefix + "-late-layout-rebuilt", neutral);
        Check(pair.Cached.StaticArt.Captures == captures + 1, "Late layout mutation did not rebuild after layout and direct observation.");
        Checks.Add(prefix + "-omitted-prepare-and-late-invalidation", true);

        foreach (Visibility visibility in new[] { Visibility.Hidden, Visibility.Collapsed, Visibility.Visible })
        {
            pair.Step(prefix + "-visibility-" + visibility, neutral, mutate: fixture => fixture.StaticBoundary.Visibility = visibility);
            pair.Step(prefix + "-visibility-settled-" + visibility, neutral);
            if (visibility != Visibility.Visible)
            {
                Check(pair.Cached.StaticArt.Captures == captures + 1, "Hidden/collapsed content captured.");
            }
        }
        pair.Cached.StaticBoundary.SetContent((MGElement)null!);
        pair.Direct.StaticBoundary.SetContent((MGElement)null!);
        pair.Step(prefix + "-empty", neutral);
        pair.Step(prefix + "-empty-settled", neutral);
        Check(pair.Cached.StaticBoundary.Content == null, "Empty boundary retained content.");
        pair.Cached.StaticBoundary.SetContent(pair.Cached.StaticArt);
        pair.Direct.StaticBoundary.SetContent(pair.Direct.StaticArt);
        pair.Step(prefix + "-reinserted-direct", neutral);
        pair.Step(prefix + "-reinserted-rebuilt", neutral);
        Checks.Add(prefix + "-hidden-collapsed-empty-and-recovery", true);

        pair.Step(prefix + "-window-translation", neutral, mutate: fixture => { fixture.Window.Left += 13; fixture.Window.Top += 11; });
        pair.Step(prefix + "-window-translation-rebuilt", neutral);
        pair.Step(prefix + "-window-resize", neutral, mutate: fixture => { fixture.Window.WindowWidth -= 25; fixture.Window.WindowHeight -= 17; });
        pair.Step(prefix + "-window-resize-rebuilt", neutral);
        Checks.Add(prefix + "-translated-resized-window", true);

        void Fallback(string name, Action<LiveSiblingFixture> mutation, MGRenderCacheBypassReason reason)
        {
            pair.Step(prefix + "-" + name, neutral, mutate: mutation);
            Check(pair.Cached.StaticBoundary.LastBypassReason == reason,
                name + " expected direct fallback " + reason + ", got " + pair.Cached.StaticBoundary.LastBypassReason);
        }
        Fallback("incoming-alpha", fixture => fixture.StaticBoundary.Opacity = 0.5f, MGRenderCacheBypassReason.Opacity);
        pair.Step(prefix + "-alpha-recovery", neutral, mutate: fixture => fixture.StaticBoundary.Opacity = 1);
        pair.Step(prefix + "-alpha-rebuilt", neutral);
        Fallback("window-scale", fixture => fixture.Window.Scale = 1.125f, MGRenderCacheBypassReason.Transform);
        pair.Step(prefix + "-transform-recovery", neutral, mutate: fixture => fixture.Window.Scale = 1);
        pair.Step(prefix + "-transform-rebuilt", neutral);
        pair.Step(prefix + "-unbounded-content", neutral, mutate: fixture => fixture.StaticArt.ClipToBounds = false);
        Check(pair.Cached.StaticBoundary.LastBypassReason != MGRenderCacheBypassReason.None, "Unbounded child qualified.");
        pair.Step(prefix + "-clip-recovery", neutral, mutate: fixture => fixture.StaticArt.ClipToBounds = true);
        pair.Step(prefix + "-clip-rebuilt", neutral);
        Checks.Add(prefix + "-alpha-transform-overflow-direct-fallback", true);

        int cachedCallbacks = 0, directCallbacks = 0;
        EventHandler<MGElement.MGElementDrawEventArgs> cachedCallback = (_, _) => cachedCallbacks++;
        EventHandler<MGElement.MGElementDrawEventArgs> directCallback = (_, _) => directCallbacks++;
        pair.Cached.StaticArt.OnEndDraw += cachedCallback;
        pair.Direct.StaticArt.OnEndDraw += directCallback;
        pair.Step(prefix + "-public-callback", neutral);
        Check(pair.Cached.StaticBoundary.LastBypassReason == MGRenderCacheBypassReason.PublicDrawCallback
            && cachedCallbacks == 1 && directCallbacks == 1, "Public callback frequency changed or Prepare invoked it.");
        pair.Cached.StaticArt.OnEndDraw -= cachedCallback;
        pair.Direct.StaticArt.OnEndDraw -= directCallback;
        pair.Step(prefix + "-callback-recovery", neutral);
        pair.Step(prefix + "-callback-rebuilt", neutral);
        Checks.Add(prefix + "-public-callback-direct-and-recovery", true);
    }
}
