using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

internal sealed partial class LiveSiblingTests : Game
{
    private readonly Dictionary<string, bool> Checks = new();
    private readonly List<object> Comparisons = new();
    private readonly List<object> Traces = new();
    private string Evidence = "";
    private bool Completed;
    private int Presented;
    private readonly int SurfaceWidth;
    private readonly int SurfaceHeight;
    private readonly float? MatrixScale;

    private LiveSiblingTests(int width = LiveSiblingFixture.Width, int height = LiveSiblingFixture.Height, float? matrixScale = null)
    {
        SurfaceWidth = width;
        SurfaceHeight = height;
        MatrixScale = matrixScale;
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height, GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using LiveSiblingTests game = new();
        game.Run();
        Check(game.Completed, "Live sibling lane did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Checks));
        Console.WriteLine("ARTIFACTS:" + game.Evidence);
        return 0;
    }

    protected override void LoadContent()
    {
        if (!MatrixScale.HasValue)
        {
            RunCases();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (MatrixScale.HasValue && !Completed)
        {
            if (Presented < 3)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                return;
            }
            Check(GraphicsDevice.PresentationParameters.BackBufferWidth == SurfaceWidth
                && GraphicsDevice.PresentationParameters.BackBufferHeight == SurfaceHeight
                && GraphicsDevice.Viewport.Bounds == new Rectangle(0, 0, SurfaceWidth, SurfaceHeight),
                "Requested matrix surface did not settle after genuine presentations.");
            RunCases();
        }
    }

    protected override void EndDraw()
    {
        base.EndDraw();
        Presented++;
    }

    private void RunCases()
    {
        Evidence = Path.Combine(Path.GetTempPath(), (MatrixScale.HasValue ? "mgui-matrix-" : "mgui-live-siblings-") + Environment.ProcessId);
        Directory.CreateDirectory(Evidence);
        foreach (float scale in MatrixScale.HasValue ? new[] { MatrixScale.Value } : new[] { 1f, 1.5f })
        {
            List<string> orderHashes = new();
            for (int order = 0; order < 3; order++)
            {
                orderHashes.Add(TestPulse(scale, order));
            }
            Check(orderHashes.Distinct().Count() == 3, "Painter-order controls did not change overlapping pixels.");
            Checks.Add("three-distinct-painter-orders-scale-" + ScaleName(scale), true);
            TestInput(scale);
            TestLayout(scale);
            TestButtonDescendantContext(scale);
            if (MatrixScale.HasValue)
            {
                TestMatrixMutations(scale);
            }
        }
        File.WriteAllText(Path.Combine(Evidence, "report.json"), JsonSerializer.Serialize(new
        {
            backend = "MonoGame DesktopGL", configuration = "Release", pulseDurationMs = 400, frameDurationMs = 20,
            checks = Checks, comparisons = Comparisons, traces = Traces,
            width = SurfaceWidth, height = SurfaceHeight, layoutScale = MatrixScale,
            genuinePresentedWarmups = MatrixScale.HasValue ? Presented : 0,
            caretClockPolicy = MatrixScale.HasValue ? "Visible for the bounded native run via public one-day BlinkRate; caret uses wall clock, input/update/volatility stay live" : "Unchanged default",
            drawableProof = MatrixScale.HasValue ? "Every draw verifies 121 submitted pixels at each physical corner; world colors advance with the matched clock" : null,
            hostSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LiveSiblingTests).Assembly.Location))),
            coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MGRenderCache).Assembly.Location)))
        }, new JsonSerializerOptions { WriteIndented = true }));
        Completed = true;
        Exit();
    }

    private string TestPulse(float scale, int order)
    {
        using Pair pair = new(this, scale, order);
        pair.Warm();
        string name = "pulse-s" + ScaleName(scale) + "-order" + order;
        Point cursor = pair.Cached.Center(pair.Cached.Score);
        for (int settle = 0; settle < 3; settle++)
        {
            pair.Step(name + "-hover" + settle, cursor);
        }
        Check(pair.Cached.Score.VisualState.IsHovered, "Pulse requires real score hover to activate ConditionalScaleTransform.");
        int staticCaptures = pair.Cached.StaticArt.Captures, frontCaptures = pair.Cached.FrontArt.Captures;
        int staticPaints = pair.Cached.StaticArt.Paints, frontPaints = pair.Cached.FrontArt.Paints;
        int updates = pair.Cached.StaticArt.Updates, liveUpdates = pair.Cached.LiveUpdates, livePaints = pair.Cached.LivePaints;
        long builds = pair.Cached.Desktop.RenderCaches.Statistics.Builds;
        pair.Cached.TriggerScore();
        pair.Direct.TriggerScore();
        Color[]? middle = null;
        List<object> pulse = new();
        for (int frame = 1; frame <= 24; frame++)
        {
            Color[] pixels = pair.Step(name + "-f" + frame, cursor, save: frame is 1 or 10 or 20 or 24);
            Check(pair.Cached.StaticArt.Captures == staticCaptures && pair.Cached.FrontArt.Captures == frontCaptures
                && pair.Cached.StaticArt.Paints == staticPaints && pair.Cached.FrontArt.Paints == frontPaints,
                "Live pulse frame " + frame + " rebuilt/repainted static: "
                + staticCaptures + "/" + pair.Cached.StaticArt.Captures + "; " + frontCaptures + "/" + pair.Cached.FrontArt.Captures
                + "; paints " + staticPaints + "/" + pair.Cached.StaticArt.Paints + "; reason " + pair.Cached.StaticBoundary.LastBypassReason);
            Check(pair.Cached.Desktop.RenderCaches.Statistics.Builds == builds, "Live pulse rebuilt a boundary.");
            bool expectedPulse = frame < 20;
            Check(pair.Cached.IsPulsing == expectedPulse, "Pulse did not stop exactly at 400 ms.");
            float renderedScale = 1;
            pair.Cached.Score.RenderScale?.TryGetScale(pair.Cached.Score.VisualState, out renderedScale);
            if (frame == 10)
            {
                Check(renderedScale > 1.17f && pair.Cached.Score.Foreground.NormalValue == Color.White,
                    "Score color/RenderScale did not reach its live midpoint.");
                middle = pixels;
            }
            if (frame >= 20)
            {
                Check(pair.Cached.Score.RenderScale == null && pair.Cached.Score.Foreground.NormalValue == Color.Gold,
                    "Pulse did not restore stable score color/scale.");
            }
            pulse.Add(new { elapsedMs = frame * 20, pair.Cached.IsPulsing, renderedScale,
                staticCaptures = pair.Cached.StaticArt.Captures, livePaints = pair.Cached.LivePaints });
        }
        Check(pair.Cached.StaticArt.Updates == updates + 24 && pair.Cached.LiveUpdates == liveUpdates + 24
            && pair.Cached.LivePaints == livePaints + 24, "Warm static/live sibling Update or live Draw frequency changed.");
        Color[] stable = pair.Cached.Paint();
        Check(middle != null && !middle.SequenceEqual(stable), "Pulse midpoint had no visible pixel effect.");
        if (MatrixScale.HasValue)
        {
            Rectangle ui = pair.Cached.Root.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
                pair.Cached.Root.LayoutBounds);
            bool changedUi = Enumerable.Range(ui.Top, ui.Height).Any(y =>
                Enumerable.Range(ui.Left, ui.Width).Any(x => middle![y * SurfaceWidth + x] != stable[y * SurfaceWidth + x]));
            Check(changedUi, "Pulse omission control changed only the world sentinel, not the UI.");
        }
        Traces.Add(new { name, pulse, staticCaptures, frontCaptures, builds, staticUpdates = 24, liveUpdates = 24, livePaints = 24 });
        Checks.Add(name + "-400ms-no-static-rebuild-exact-pixels-live-updates", true);
        return PixelHash(middle!);
    }

    private void TestInput(float scale)
    {
        using Pair pair = new(this, scale, 1);
        pair.Warm();
        LiveSiblingFixture cached = pair.Cached;
        string name = "input-s" + ScaleName(scale);
        Point neutral = cached.Root.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
            new Point(cached.Root.LayoutBounds.Right - 5, cached.Root.LayoutBounds.Bottom - 5));
        for (int settle = 0; settle < 3; settle++)
        {
            pair.Step(name + "-enter-root" + settle, neutral);
        }
        int staticCaptures = cached.StaticArt.Captures, frontCaptures = cached.FrontArt.Captures;
        int staticPaints = cached.StaticArt.Paints, frontPaints = cached.FrontArt.Paints;
        int ticks = cached.LiveUpdates;
        void Step(string stage, Point cursor, bool left = false, bool right = false, int wheel = 0,
            KeyboardState keyboard = default, Action<LiveSiblingFixture>? mutate = null)
        {
            pair.Step(name + "-" + stage, cursor, left, right, wheel, keyboard, mutate, save: true);
            Check(cached.StaticArt.Captures == staticCaptures && cached.FrontArt.Captures == frontCaptures
                && cached.StaticArt.Paints == staticPaints && cached.FrontArt.Paints == frontPaints,
                stage + " rebuilt static siblings.");
        }
        Point button = cached.Center(cached.Button);
        Check(button.X > cached.Window.Left && button.Y > cached.Window.Top, "Input fixture lost nonzero window offset.");
        Step("hover", button);
        Check(cached.Button.VisualState.IsHovered, "Real mouse did not hover live button.");
        Step("press", button, left: true);
        Check(cached.Button.VisualState.IsPressed, "Real mouse did not press live button.");
        Step("release-selected", button);
        Check(cached.Events.Count(value => value == "button") == 1 && cached.Button.IsSelected,
            "Live button release event/selection was lost.");
        Step("selected-pixels", button);
        Check(cached.Button.VisualState.IsSelected, "Selected button did not update visual state.");
        Step("disabled", button, mutate: fixture => fixture.Button.IsEnabled = false);
        Step("disabled-press", button, left: true);
        Step("disabled-release", button);
        Check(cached.Button.VisualState.IsDisabled && cached.Events.Count(value => value == "button") == 1,
            "Disabled control received a click.");
        Step("reenabled", neutral, mutate: fixture => fixture.Button.IsEnabled = true);
        Point toggle = cached.Center(cached.Toggle);
        Step("toggle-hover", toggle);
        Step("toggle-press", toggle, left: true);
        Step("toggle-release", toggle);
        Check(cached.Toggle.IsChecked, "Real toggle click did not change checked state.");
        Checks.Add(name + "-hover-press-selection-disabled-toggle", true);

        Point thumb = cached.SliderPosition(0.2f), end = cached.SliderPosition(0.75f);
        Step("slider-hover", thumb);
        Check(cached.Slider.IsHoveringThumb, "Derived scale/offset thumb coordinate missed.");
        Step("slider-press", thumb, left: true);
        Step("slider-drag-start", new(thumb.X + 4, thumb.Y), left: true);
        Step("slider-drag", end, left: true);
        Check(cached.Slider.IsDraggingThumb && cached.Slider.Value > 65, "Real slider drag did not move thumb.");
        Step("slider-release", end);
        Check(!cached.Slider.IsDraggingThumb && cached.Events.Any(value => value.StartsWith("slider:")),
            "Slider drag did not release or notify.");
        Checks.Add(name + "-scaled-slider-drag", true);

        Point text = cached.Center(cached.TextBox);
        Step("text-hover", text);
        Step("text-press", text, left: true);
        Step("text-release", text);
        Check(cached.Desktop.FocusedKeyboardHandler == cached.TextBox, "Real text click did not set keyboard focus.");
        Step("key-a", text, keyboard: new(Keys.A));
        Step("key-a-up", text);
        Step("key-b", text, keyboard: new(Keys.B));
        Step("key-b-up", text);
        Check(cached.TextBox.Text == "ab", "Focused text box did not receive real keyboard text.");
        Step("select-all", text, keyboard: new(Keys.LeftControl, Keys.A));
        Check(cached.TextBox.CurrentSelection.HasValue && cached.TextBox.CurrentSelection.Value.Length == 2,
            "Keyboard selection was lost.");
        Step("select-all-up", text);
        Step("replace-selection", text, keyboard: new(Keys.C));
        Step("replace-selection-up", text);
        Check(cached.TextBox.Text == "c", "Keyboard replacement did not preserve text selection semantics.");
        Checks.Add(name + "-mouse-focus-keyboard-selection", true);

        Step("tooltip-hover", button);
        for (int frame = 0; frame < 4; frame++)
        {
            Step("tooltip-wait" + frame, button);
        }
        Check(cached.Desktop.ActiveToolTip == cached.Tip, "Real hover did not open tooltip after deterministic delay.");
        Step("menu-press", button, right: true);
        Step("menu-release", button);
        Check(cached.Desktop.ActiveContextMenu == cached.Menu, "Right click did not open live context menu.");
        Step("menu-layout", button);
        Point menu = cached.Center(cached.Menu.Items[0]);
        Step("menu-item-hover", menu);
        Step("menu-item-press", menu, left: true);
        Step("menu-item-release", menu);
        Check(cached.Events.Count(value => value == "menu") == 1 && cached.Desktop.ActiveContextMenu == null,
            "Live context menu action/close was lost.");
        Checks.Add(name + "-tooltip-and-context-menu", true);

        Point scroll = cached.Center(cached.Scroll);
        Step("scroll-hover", scroll);
        Step("scroll-wheel", scroll, wheel: -120);
        Check(cached.Scroll.VerticalOffset > 0, "Real wheel input did not scroll.");
        Step("scroll-offset-settled", scroll, wheel: -120);
        Rectangle screenViewport = cached.Scroll.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
            cached.Scroll.ContentViewport);
        MGButton row = cached.Rows.First(item =>
        {
            Rectangle screenBounds = item.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, item.LayoutBounds);
            return screenBounds.Width > 0 && screenBounds.Height > 0 && screenViewport.Contains(screenBounds)
                && item.ActualLayoutBounds.Contains(screenBounds);
        });
        Point rowPoint = cached.Center(row);
        string expectedRowEvent = "row:" + cached.Rows.IndexOf(row);
        int rowEventsBefore = cached.Events.Count(value => value == expectedRowEvent);
        Step("row-hover", rowPoint, wheel: -120);
        Step("row-press", rowPoint, left: true, wheel: -120);
        Step("row-release", rowPoint, wheel: -120);
        Check(cached.Events.Count(value => value == expectedRowEvent) == rowEventsBefore + 1,
            "Visible scrolled child lost input or clicked the wrong row: " + expectedRowEvent + "; " + string.Join(",", cached.Events));
        Checks.Add(name + "-wheel-scroll-and-visible-row-input", true);

        Point inside = cached.Center(cached.InsideButton);
        int insideCaptures = cached.InteractiveArt.Captures, insidePaints = cached.InteractiveArt.Paints;
        long hits = cached.Desktop.RenderCaches.Statistics.Hits;
        Step("cached-button-hover", inside, wheel: -120);
        Check(cached.InsideButton.VisualState.IsHovered && cached.InteractiveArt.Paints > insidePaints,
            "Warm interactive boundary did not invalidate and directly paint real hover.");
        Check(cached.InteractiveArt.Captures == insideCaptures, "Hover captured before observing its changed generation.");
        Step("cached-button-build", inside, wheel: -120);
        Check(cached.InteractiveArt.Captures == insideCaptures + 1, "Hovered interactive button did not rebuild after direct observation.");
        insidePaints = cached.InteractiveArt.Paints;
        int updates = cached.InteractiveArt.Updates;
        for (int frame = 0; frame < 3; frame++)
        {
            Step("cached-button-hit" + frame, inside, wheel: -120);
        }
        Check(cached.InteractiveArt.Paints == insidePaints && cached.InteractiveArt.Updates == updates + 3
            && cached.Desktop.RenderCaches.Statistics.Hits >= hits + 3,
            "Warm interactive hit skipped Update or repainted unchanged content.");
        Step("cached-button-press", inside, left: true, wheel: -120);
        Check(cached.InsideButton.VisualState.IsPressed && cached.InteractiveArt.Paints > insidePaints,
            "Warm interactive button did not directly repaint pressed generation.");
        Step("cached-button-release", inside, wheel: -120);
        Check(cached.Events.Count(value => value == "inside-button") == 1, "Cached button lost release event.");
        Checks.Add(name + "-warm-boundary-control-input-update-and-invalidation", true);
        for (int frame = 0; frame < 3; frame++)
        {
            pair.Step(name + "-ancestor-scale-unit" + frame, button, wheel: -120,
                mutate: frame == 0 ? fixture => fixture.Root.RenderScale = new(1.1f, 1) : null);
        }
        int paintsBeforeScale = cached.StaticArt.Paints;
        int capturesBeforeScale = cached.StaticArt.Captures;
        pair.Step(name + "-ancestor-scale-press", button, left: true, wheel: -120, save: true);
        Check(cached.StaticArt.Paints == paintsBeforeScale + 1 && cached.StaticArt.Captures == capturesBeforeScale
            && cached.StaticBoundary.LastBypassReason == MGRenderCacheBypassReason.Transform,
            "Secondary state did not invalidate inherited nonunit scale or failed to bypass.");
        pair.Step(name + "-ancestor-scale-release", button, wheel: -120);
        pair.Step(name + "-ancestor-scale-recovered", button, wheel: -120);
        Check(cached.StaticArt.Captures == capturesBeforeScale + 1,
            "Inherited scale recovery failed to rebuild an observed unit-scale generation.");
        Checks.Add(name + "-secondary-state-retains-inherited-scale-invalidation", true);
        Check(cached.LivePaints == cached.LiveUpdates && pair.Direct.LivePaints == pair.Direct.LiveUpdates,
            "Input replay skipped an ordinary live update or draw.");
        Traces.Add(new { name, frames = cached.LiveUpdates - ticks, clockMs = cached.Clock.TotalMilliseconds,
            staticCaptures, frontCaptures, insideCaptures = cached.InteractiveArt.Captures,
            livePaints = cached.LivePaints, liveUpdates = cached.LiveUpdates, cached.Events });
    }

    private void TestLayout(float scale)
    {
        using Pair pair = new(this, scale, 1);
        MGRectangle cachedLeading = pair.Cached.UseSizeDrivenLayout();
        MGRectangle directLeading = pair.Direct.UseSizeDrivenLayout();
        string name = "layout-s" + ScaleName(scale);
        for (int frame = 0; frame < 5; frame++)
        {
            pair.Step(name + "-warm" + frame, new(2, 2));
        }
        int captures = pair.Cached.StaticArt.Captures, paints = pair.Cached.StaticArt.Paints;
        int oldLeft = pair.Cached.StaticBoundary.LayoutBounds.Left;
        long hits = pair.Cached.Desktop.RenderCaches.Statistics.Hits;
        cachedLeading.Width = 60;
        directLeading.Width = 60;
        pair.Step(name + "-changed-direct", new(2, 2), save: true);
        Check(pair.Cached.StaticBoundary.LayoutBounds.Left > oldLeft && pair.Cached.StaticArt.Paints == paints + 1
            && pair.Cached.StaticArt.Captures == captures && pair.Cached.Desktop.RenderCaches.Statistics.Hits == hits,
            "Sibling content-size change reused static pixels at old geometry or captured before observation.");
        pair.Step(name + "-rebuilt", new(2, 2), save: true);
        Check(pair.Cached.StaticArt.Captures == captures + 1 && pair.Cached.Desktop.RenderCaches.Statistics.Hits == hits + 1,
            "Size-driven changed geometry did not rebuild after direct observation.");
        Traces.Add(new { name, oldLeft, newLeft = pair.Cached.StaticBoundary.LayoutBounds.Left,
            builds = pair.Cached.Desktop.RenderCaches.Statistics.Builds });
        Checks.Add(name + "-real-sibling-content-size-moves-and-rebuilds-static", true);
    }

    private void TestButtonDescendantContext(float scale)
    {
        using Pair pair = new(this, scale, 1);
        (MGRenderCache Boundary, LiveSiblingFixture.PaintCounter Art, MGTextBlock Text) Create(LiveSiblingFixture fixture, bool enabled)
        {
            fixture.Button.ToolTip = null;
            fixture.Button.ContextMenu = null;
            fixture.Button.PreferredWidth = 130;
            fixture.Button.PreferredHeight = 50;
            fixture.Button.PressedContentOffset = new(3, 2);
            fixture.Button.DefaultTextForeground = new(Color.White, Color.Lime, Color.Gray);
            MGTextBlock text = new(fixture.Window, "INHERITED", null, 12);
            LiveSiblingFixture.PaintCounter art = new(fixture.Window);
            art.SetContent(text);
            MGRenderCache boundary = new(fixture.Window) { IsCacheEnabled = enabled, IsHitTestVisible = false };
            boundary.SetContent(art);
            fixture.Button.SetContent(boundary);
            return (boundary, art, text);
        }
        var cached = Create(pair.Cached, true);
        var direct = Create(pair.Direct, false);
        string name = "button-context-s" + ScaleName(scale);
        Point cursor = new(2, 2);
        for (int frame = 0; frame < 5; frame++)
        {
            pair.Step(name + "-warm" + frame, cursor);
        }
        cursor = pair.Cached.Center(pair.Cached.Button);
        for (int frame = 0; frame < 3; frame++)
        {
            pair.Step(name + "-hover" + frame, cursor);
        }
        Check(cached.Art.Captures > 0, "Nested button-content boundary did not warm.");
        cached.Art.InvalidateVisual();
        direct.Art.InvalidateVisual();
        pair.Step(name + "-pending-hover-observation", cursor);
        Check(pair.Cached.Desktop.RenderCaches.PendingCount > 0, "Button context test did not queue a prior offset observation.");
        int captures = cached.Art.Captures, paints = cached.Art.Paints;
        ulong revision = cached.Art.VisualRevision;
        pair.Step(name + "-press-rejects-pending-old-offset", cursor, left: true, save: true, afterUpdate: fixture =>
        {
            if (ReferenceEquals(fixture, pair.Cached))
            {
                Check(cached.Art.VisualRevision > revision && cached.Art.Captures == captures,
                    "Button secondary offset failed to retire descendant generation before Prepare.");
            }
        });
        Check(cached.Art.Captures == captures && cached.Art.Paints == paints + 1,
            "Prepare captured the old pending button-content offset or failed to draw changed content directly.");
        pair.Step(name + "-pressed-rebuilt", cursor, left: true);
        Check(cached.Art.Captures == captures + 1, "New pressed-content offset did not rebuild after direct observation.");
        captures = cached.Art.Captures;
        revision = cached.Art.VisualRevision;
        pair.Step(name + "-offset-setter-while-pressed", cursor, left: true, save: true,
            mutate: fixture => fixture.Button.PressedContentOffset = new(6, 4), afterUpdate: fixture =>
            {
                if (ReferenceEquals(fixture, pair.Cached))
                {
                    Check(cached.Art.VisualRevision > revision, "Active offset setter failed to invalidate descendants before Prepare.");
                }
            });
        Check(cached.Art.Captures == captures, "Changed active offset captured an unobserved generation.");
        pair.Step(name + "-new-pressed-offset-rebuilt", cursor, left: true);
        Check(cached.Art.Captures == captures + 1, "Changed active offset did not rebuild: " + cached.Boundary.LastBypassReason + "; " + cached.Boundary.LayoutBounds);
        pair.Step(name + "-release", cursor, save: true);
        for (int frame = 0; frame < 3; frame++)
        {
            pair.Step(name + "-selected-foreground" + frame, cursor, save: frame == 2);
        }
        Check(pair.Cached.Button.VisualState.IsSelected && cached.Text.ActualForeground == Color.Lime,
            "Selected primary state failed to update inherited foreground inside nested cached content.");
        captures = cached.Art.Captures;
        paints = cached.Art.Paints;
        pair.Step(name + "-selected-warm-hit", cursor);
        Check(cached.Art.Captures == captures && cached.Art.Paints == paints,
            "Selected foreground did not recover a warm boundary hit.");
        Checks.Add(name + "-pending-offset-context-and-primary-foreground", true);
    }

    private void Compare(string name, Color[] direct, Color[] cached, bool save, long matchedClockTicks, string matchedState)
    {
        int different = 0;
        int[] maxima = new int[4];
        for (int index = 0; index < direct.Length; index++)
        {
            if (direct[index] != cached[index])
            {
                different++;
                maxima[0] = Math.Max(maxima[0], Math.Abs(direct[index].R - cached[index].R));
                maxima[1] = Math.Max(maxima[1], Math.Abs(direct[index].G - cached[index].G));
                maxima[2] = Math.Max(maxima[2], Math.Abs(direct[index].B - cached[index].B));
                maxima[3] = Math.Max(maxima[3], Math.Abs(direct[index].A - cached[index].A));
            }
        }
        string? directImage = null, cachedImage = null, differenceImage = null;
        if (MatrixScale.HasValue)
        {
            directImage = Path.Combine(Evidence, name + "-direct.png");
            cachedImage = Path.Combine(Evidence, name + "-cached.png");
            WriteMatrixPng(directImage, direct);
            if (different == 0)
            {
                File.Copy(directImage, cachedImage, overwrite: true);
                differenceImage = Path.Combine(Evidence, $"exact-difference-{SurfaceWidth}x{SurfaceHeight}.png");
                if (!File.Exists(differenceImage))
                {
                    WriteMatrixPng(differenceImage, Enumerable.Repeat(Color.Black, direct.Length).ToArray());
                }
            }
            else
            {
                WriteMatrixPng(cachedImage, cached);
                differenceImage = Path.Combine(Evidence, name + "-difference-x64.png");
                WriteMatrixPng(differenceImage, direct.Select((pixel, index) => new Color(
                    Math.Min(255, Math.Abs(pixel.R - cached[index].R) * 64),
                    Math.Min(255, Math.Abs(pixel.G - cached[index].G) * 64),
                    Math.Min(255, Math.Abs(pixel.B - cached[index].B) * 64), 255)).ToArray());
            }
        }
        else if (save || different > 0)
        {
            using Texture2D texture = new(GraphicsDevice, SurfaceWidth, SurfaceHeight);
            foreach (var item in new[] { ("direct", direct), ("cached", cached) })
            {
                string path = Path.Combine(Evidence, name + "-" + item.Item1 + ".png");
                if (item.Item1 == "direct") { directImage = path; } else { cachedImage = path; }
                texture.SetData(item.Item2);
                using FileStream file = File.Create(path);
                texture.SaveAsPng(file, SurfaceWidth, SurfaceHeight);
            }
        }
        Comparisons.Add(new { name, different, maxima, allowedErrorsRGBA = new[] { 0, 0, 0, 0 },
            matchedClockTicks, matchedState = JsonSerializer.Deserialize<JsonElement>(matchedState),
            directImage, cachedImage, differenceImage, directSha256 = PixelHash(direct), sha256 = PixelHash(cached) });
        Check(different == 0, name + " exact RGBA mismatch " + string.Join(",", maxima) + "; evidence " + Evidence);
    }

    private static string PixelHash(Color[] pixels)
    {
        byte[] bytes = new byte[pixels.Length * 4];
        for (int index = 0; index < pixels.Length; index++)
        {
            bytes[index * 4] = pixels[index].R;
            bytes[index * 4 + 1] = pixels[index].G;
            bytes[index * 4 + 2] = pixels[index].B;
            bytes[index * 4 + 3] = pixels[index].A;
        }
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
    private static string ScaleName(float scale) => scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Pair : IDisposable
    {
        private readonly LiveSiblingTests Tests;
        internal LiveSiblingFixture Cached { get; }
        internal LiveSiblingFixture Direct { get; }
        internal Pair(LiveSiblingTests tests, float scale, int order)
        {
            Tests = tests;
            Cached = new(tests, scale, order, cached: true, tests.SurfaceWidth, tests.SurfaceHeight, tests.MatrixScale.HasValue);
            Direct = new(tests, scale, order, cached: false, tests.SurfaceWidth, tests.SurfaceHeight, tests.MatrixScale.HasValue);
        }
        internal void Warm()
        {
            for (int frame = 0; frame < 5; frame++)
            {
                Step("warm-s" + ScaleName(Cached.Scale) + "-" + Tests.Comparisons.Count, new(2, 2));
            }
            Check(Cached.StaticArt.Captures == 1 && Cached.FrontArt.Captures == 1 && Cached.InteractiveArt.Captures == 1,
                "Static/interactive native boundaries did not warm: " + string.Join(",", Cached.Boundaries.Select(item => item.LastBypassReason)));
        }
        internal Color[] Step(string name, Point cursor, bool left = false, bool right = false, int wheel = 0,
            KeyboardState keyboard = default, Action<LiveSiblingFixture>? mutate = null, bool save = false, Action<LiveSiblingFixture>? afterUpdate = null, bool prepare = true, Action<LiveSiblingFixture>? afterPrepare = null)
        {
            mutate?.Invoke(Cached);
            mutate?.Invoke(Direct);
            MouseState mouse = new(cursor.X, cursor.Y, wheel, left ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Cached.Mouse = Direct.Mouse = mouse;
            Cached.Keyboard = Direct.Keyboard = keyboard;
            Cached.Tick(prepare: false);
            Direct.Tick(prepare: false);
            afterUpdate?.Invoke(Cached);
            afterUpdate?.Invoke(Direct);
            if (prepare)
            {
                Cached.Desktop.PrepareRenderCaches();
                Direct.Desktop.PrepareRenderCaches();
            }
            afterPrepare?.Invoke(Cached);
            afterPrepare?.Invoke(Direct);
            Color[] pixels = Cached.Paint();
            string state = Snapshot(Cached);
            Check(Cached.Clock == Direct.Clock && Cached.Desktop.InputTracker.Mouse.CurrentState == mouse
                && Direct.Desktop.InputTracker.Mouse.CurrentState == mouse
                && Cached.Desktop.InputTracker.Keyboard.CurrentState == keyboard
                && Direct.Desktop.InputTracker.Keyboard.CurrentState == keyboard,
                name + " did not replay identical host input/time through InputTracker.");
            Check(state == Snapshot(Direct), name + " cached/direct input snapshots diverged: " + state + " / " + Snapshot(Direct));
            Tests.Compare(name, Direct.Paint(), pixels, save, Cached.Clock.Ticks, state);
            return pixels;
        }
        private static string Snapshot(LiveSiblingFixture fixture) => JsonSerializer.Serialize(new
        {
            clockTicks = fixture.Clock.Ticks,
            mouse = new { fixture.Mouse.X, fixture.Mouse.Y, fixture.Mouse.ScrollWheelValue,
                left = fixture.Mouse.LeftButton.ToString(), right = fixture.Mouse.RightButton.ToString() },
            keyboard = fixture.Keyboard.GetPressedKeys().Select(key => key.ToString()).ToArray(),
            button = fixture.Button.VisualState, fixture.Button.IsSelected,
            fixture.Button.IsEnabled, fixture.Toggle.IsChecked, slider = fixture.Slider.Value, fixture.Slider.IsDraggingThumb,
            fixture.Slider.IsHoveringThumb, fixture.TextBox.Text, selection = fixture.TextBox.CurrentSelection?.ToString(),
            focused = fixture.Desktop.FocusedKeyboardHandler == fixture.TextBox,
            caretBlinkRateTicks = fixture.TextBox.Caret.BlinkRate.Ticks,
            tooltip = fixture.Desktop.ActiveToolTip == fixture.Tip, menu = fixture.Desktop.ActiveContextMenu == fixture.Menu,
            scroll = fixture.Scroll.VerticalOffset, inside = fixture.InsideButton.VisualState, fixture.Events,
            scoreBounds = fixture.Score.LayoutBounds, buttonBounds = fixture.Button.LayoutBounds,
            staticBounds = fixture.StaticBoundary.LayoutBounds, insideBounds = fixture.InsideButton.LayoutBounds,
            fixture.LiveUpdates
        });
        public void Dispose()
        {
            Cached.Dispose();
            Direct.Dispose();
        }
    }
}
