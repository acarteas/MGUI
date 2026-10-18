using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class EligibilityTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;

    private EligibilityTests()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640,
            PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false
        };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using EligibilityTests game = new();
        game.Run();
        Check(game.Completed, "Eligibility checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; native RGBA readback; renderer recorded by test-native.py.");
        return 0;
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 50, 240, 180) { WindowStyle = WindowStyle.None, Padding = new(0) };
        desktop.Windows.Add(window);
        using RenderTarget2D target = new(GraphicsDevice, 640, 480);
        void Arrange(MGElement element)
        {
            window.SetContent(element);
            for (int settle = 0; settle < 3; settle++)
            {
                fixture.Update(++Frame);
            }
        }
        Color[] Paint(MGElement element, bool capture)
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.ScissorRectangle = target.Bounds;
            GraphicsDevice.Clear(Color.Transparent);
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                if (capture)
                {
                    transaction.ConfigureCacheCapture(Point.Zero);
                }
                element.Draw(new(new(TimeSpan.Zero, transaction, 1), element.VisualState, Point.Zero));
            }
            GraphicsDevice.SetRenderTarget(null);
            Color[] pixels = new Color[640 * 480];
            target.GetData(pixels);
            return pixels;
        }
        void Eligible(MGElement element)
        {
            MGRenderEligibilitySnapshot snapshot = element.GetRenderEligibilitySnapshot();
            Check(snapshot.IsEligible, element.GetType().Name + " unexpectedly ineligible: " + snapshot);
        }

        UnknownRectangle unknown = new(window);
        Arrange(unknown);
        Check(unknown.GetRenderEligibilitySnapshot().UnsupportedCount == 1, "Unknown built-in subclass inherited support.");
        ManualPainter manual = new(window);
        Arrange(manual);
        Eligible(manual);
        Color[] first = Paint(manual, false);
        Check(first.SequenceEqual(Paint(manual, true)), "Explicit custom painter capture changed pixels.");
        ulong layout = manual.LayoutRevision;
        MGRenderRevisionSnapshot previous = manual.GetRenderRevisionSnapshot();
        manual.Tint = Color.CornflowerBlue;
        manual.InvalidateVisual();
        Check(!manual.IsRenderRevisionCurrent(previous) && manual.LayoutRevision == layout, "Manual custom invalidation missed or forced layout.");
        Color[] changed = Paint(manual, false);
        Check(!first.SequenceEqual(changed) && changed.SequenceEqual(Paint(manual, true)), "Manually invalidated custom painter did not capture new pixels.");
        Results["exact-type-and-custom-manual-capture"] = true;

        int begins = 0;
        int endings = 0;
        int ends = 0;
        EventHandler<MGElement.MGElementDrawEventArgs> begin = (_, _) => begins++;
        EventHandler<MGElement.MGElementDrawEventArgs> ending = (_, _) => endings++;
        EventHandler<MGElement.MGElementDrawEventArgs> end = (_, _) => ends++;
        manual.OnBeginDraw += begin;
        manual.OnEndingDraw += ending;
        manual.OnEndDraw += end;
        Check(manual.GetRenderEligibilitySnapshot().PublicDrawCallbackCount == 3, "Public hooks did not block capture eligibility.");
        int updates = manual.Updates;
        for (int iteration = 0; iteration < 5; iteration++)
        {
            fixture.Update(++Frame);
            Paint(manual, false);
            Paint(manual, true);
        }
        Check(begins == 5 && endings == 5 && ends == 5 && manual.Updates == updates + 5,
            "Normal draw/update callback frequency changed or capture invoked public hooks.");
        manual.OnBeginDraw -= begin;
        manual.OnEndingDraw -= ending;
        manual.OnEndDraw -= end;
        Eligible(manual);
        Results["public-hook-frequency-capture-suppression-recovery"] = true;

        MGContentPresenter parent = new(window);
        MGTimer timer = new(window, TimeSpan.FromSeconds(30));
        parent.SetContent(timer);
        Arrange(parent);
        Eligible(parent);
        timer.Resume();
        Check(parent.GetRenderEligibilitySnapshot().VolatileCount > 0, "Running timer did not block flattened parent.");
        timer.IsRenderVolatile = false;
        Check(parent.GetRenderEligibilitySnapshot().VolatileCount > 0, "User false suppressed intrinsic timer volatility.");
        TimeSpan beforeTime = timer.RemainingDuration;
        fixture.Update(++Frame);
        Paint(parent, false);
        Check(timer.RemainingDuration < beforeTime, "Volatility bypass stopped the normal timer update.");
        timer.Pause();
        fixture.Update(++Frame);
        Eligible(parent);
        timer.IsRenderVolatile = true;
        Check(parent.GetRenderEligibilitySnapshot().VolatileCount > 0, "User volatility was not maintained.");
        parent.SetContent(new MGSpacer(window, 10, 10));
        fixture.Update(++Frame);
        Eligible(parent);
        Results["intrinsic-user-volatility-update-and-removal"] = true;

        MGRectangle stable = new(window, 20, 20, Color.Black, 0, Color.Red);
        MGTreeViewItem exceptional = new(window);
        MGContextualContentPresenter contextual = new(window, true, stable, exceptional);
        Arrange(contextual);
        Eligible(contextual);
        contextual.Value = false;
        Check(contextual.GetRenderEligibilitySnapshot().UnsupportedCount > 0, "Tree item with post-base paint qualified.");
        contextual.Value = true;
        fixture.Update(++Frame);
        Eligible(contextual);
        MGToolTip tooltip = new(window, stable, 80, 40);
        MGContextMenu menu = new(window);
        stable.ToolTip = tooltip;
        stable.ContextMenu = menu;
        fixture.Update(++Frame);
        Eligible(stable);
        Check(window.GetRenderEligibilitySnapshot().UnsupportedCount > 0
            && tooltip.GetRenderEligibilitySnapshot().UnsupportedCount > 0
            && menu.GetRenderEligibilitySnapshot().UnsupportedCount > 0, "Live window/popup roots qualified as ordinary content.");
        Results["inactive-exceptional-content-and-popup-root-isolation"] = true;

        MGUniformGrid grid = new(window, 1, 1, new(30, 30));
        Arrange(grid);
        Eligible(grid);
        EventHandler<MGUniformGrid.RenderCellArgs> cell = (_, _) => { };
        grid.OnRenderCell += cell;
        Check(grid.GetRenderEligibilitySnapshot().PublicDrawCallbackCount == 1, "Control-specific public painter was ignored.");
        grid.OnRenderCell -= cell;
        Eligible(grid);
        MGContextMenuSeparator separator = menu.AddSeparator();
        separator.UpdateLayout(new(50, 50, 60, 10));
        Check(separator.GetRenderEligibilitySnapshot().UnsupportedCount == 0, "Static separator unexpectedly unsupported.");
        separator.ComputeIsVisible = () => true;
        Check(separator.GetRenderEligibilitySnapshot().UnsupportedCount == 1, "Opaque visibility delegate qualified.");
        separator.ComputeIsVisible = null;
        Check(separator.GetRenderEligibilitySnapshot().UnsupportedCount == 0, "Cleared visibility delegate stayed unsupported.");
        MGOverlayHost overlay = new(window);
        Check(overlay.GetRenderEligibilitySnapshot().UnsupportedCount > 0, "Cross-owner overlay tint qualified.");
        MGResizeGrip external = new(window, stable);
        Check(external.GetRenderEligibilitySnapshot().UnsupportedCount > 0, "External-host resize grip qualified.");
        Results["control-specific-public-and-opaque-state-facts"] = true;

        MGTextBlock text = new(window, "[action=demo]click[/action]");
        Arrange(text);
        Check(text.HasInteractiveTextRuns && text.GetRenderEligibilitySnapshot().UnsupportedCount > 0,
            "Interactive text's draw-built input metadata qualified.");
        text.SetText("plain", true);
        Eligible(text);
        Results["interactive-text-parse-time-recovery"] = true;

        desktop.RenderCaches.Clear();
        MGCachedFillBrush background = new(Color.DarkBlue.AsFillBrush());
        UnknownPresenter backgroundOwner = new(window) { BackgroundBrush = new(background) };
        backgroundOwner.SetContent(new MGRectangle(window, 20, 20, Color.Black, 0, Color.Red) { IsRenderVolatile = true });
        Arrange(backgroundOwner);
        Check(!backgroundOwner.GetRenderEligibilitySnapshot().IsEligible, "Unknown owner/live child unexpectedly flattened.");
        for (int settle = 0; settle < 4; settle++)
        {
            fixture.Update(++Frame);
            desktop.PrepareRenderCaches();
            Paint(backgroundOwner, false);
        }
        long hits = desktop.RenderCaches.Statistics.Hits;
        fixture.Update(++Frame);
        desktop.PrepareRenderCaches();
        Paint(backgroundOwner, false);
        Check(desktop.RenderCaches.Statistics.Hits > hits, "Unrelated unknown/volatile subtree disqualified background-only brush.");
        backgroundOwner.IsRenderVolatile = true;
        hits = desktop.RenderCaches.Statistics.Hits;
        fixture.Update(++Frame);
        desktop.PrepareRenderCaches();
        Paint(backgroundOwner, false);
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Background-only brush ignored relevant owner volatility.");
        Results["background-only-qualification-remains-local"] = true;
        desktop.DisposeRenderCaches();
        Completed = true;
    }

    protected override void Update(GameTime gameTime) => Exit();

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class UnknownRectangle : MGRectangle
    {
        public UnknownRectangle(MGWindow window) : base(window, 20, 20, Color.Black, 0, Color.Red) { }
    }

    private sealed class UnknownPresenter : MGContentPresenter
    {
        public UnknownPresenter(MGWindow window) : base(window) { }
    }

    private sealed class ManualPainter : MGElement
    {
        public Color Tint = Color.Red;
        public int Updates;
        protected override bool SupportsRenderCaching => true;
        public ManualPainter(MGWindow window) : base(window, MGElementType.Rectangle) { }
        public override void UpdateSelf(ElementUpdateArgs args) { Updates++; }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
            => new MGSolidFillBrush(Tint).Draw(args, this, bounds);
    }
}
