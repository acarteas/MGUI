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

internal readonly record struct StabilityState(int Frame, long ClockTicks, string Score, string StaticText,
    int PulseFrame, bool Hovered, bool Pressed, int Clicks, float Scroll, int Width, int Height,
    float Scale, bool Visible, bool Enabled, bool Reparented, int Operation, float ScoreScale, uint ScoreColor, uint SourceColor);

// Deliberately separate from the six retained T01 fixtures and their state fingerprints.
internal sealed class StabilityFixture : IRenderHost, IDisposable
{
    private readonly Game Game;
    private readonly StabilityOptions Options;
    private readonly MainRenderer Renderer;
    private readonly FontSystem Font;
    private readonly MGWindow Window;
    private readonly MGTextBlock Score;
    private readonly MGTextBlock StaticText;
    private readonly MGButton Button;
    private readonly MGScrollViewer Scroll;
    private readonly MGContentPresenter FirstHost;
    private readonly MGContentPresenter AlternateHost;
    private readonly List<MGRenderCache> Boundaries = new();
    private readonly List<MGCachedFillBrush> Backgrounds = new();
    private readonly List<Probe> Probes = new();
    private readonly List<MGTextBlock> Cells = new();
    private readonly List<CountingFill> Sources = new();
    private readonly RenderTargetBinding[] CaptureTarget = new RenderTargetBinding[1];
    private MouseState Mouse;
    private TimeSpan Clock;
    private int Clicks;
    private int LastOperation = -1;
    private bool Reparented;
    private bool Visible = true;
    private bool Enabled = true;
    private float Scale;
    private long IdleDiscoveryBaseline;
    private long IdleReadBaseline;
    private long IdleBuildBaseline;
    private int LatePaintBaseline;
    private int LateSourcePaintBaseline;
    private bool WarmEstablished;
    internal MGDesktop Desktop { get; }
    internal StabilityState State { get; private set; }
    internal long InFlightChecks { get; private set; }
    internal long RetirementChecks { get; private set; }
    internal long LateMutationChecks { get; private set; }
    internal long IdleChecks { get; private set; }
    internal long DiscoveryVisits
    {
        get
        {
            long total = 0;
            foreach (Probe probe in Probes)
            {
                total += probe.Discoveries;
            }
            return total;
        }
    }
    internal long SourceReads
    {
        get
        {
            long total = 0;
            foreach (CountingFill source in Sources)
            {
                total += source.Reads;
            }
            return total;
        }
    }
    internal long MaximumBytes { get; private set; }
    internal int MaximumEntries { get; private set; }
    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;

    internal StabilityFixture(Game game, StabilityOptions options)
    {
        Game = game;
        Options = options;
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        Font = new();
        Font.AddFont(bytes);
        FontStashSharpTextEngine textEngine = new();
        textEngine.AddFontSystem("NotoSans", CustomFontStyles.Normal, Font, bytes);
        Renderer = new(this, new FontManager("NotoSans")) { TextEngine = textEngine };
        Desktop = new(Renderer);
        Scale = options.Scale;
        Desktop.SetUniformUIScale(Scale);
        Window = new(Desktop, 32, 32, options.Width - 64, options.Height - 64)
        {
            WindowStyle = WindowStyle.None, Padding = new(6),
            BackgroundBrush = new(new MGSolidFillBrush(new Color(12, 23, 38))), ClipToBounds = true
        };
        Desktop.Windows.Add(Window);
        MGOverlayPanel root = new(Window);
        Window.SetContent(root);
        MGStackPanel art = new(Window, Orientation.Vertical) { Spacing = 4, Padding = new(0) };
        Place(root, art, 8, 8, 500, 125);
        FirstHost = new(Window) { Padding = new(0) };
        AlternateHost = new(Window) { Padding = new(0) };
        MGOverlayPanel firstRow = new(Window) { PreferredHeight = 38 };
        Place(firstRow, FirstHost, 0, 0, 480, 38);
        Place(firstRow, AlternateHost, 8, 0, 480, 38);
        art.TryAddChild(firstRow);
        MGTextBlock? first = null;
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            MGStackPanel row = new(Window, Orientation.Horizontal) { Spacing = 4, Padding = new(0) };
            for (int column = 0; column < 4; column++)
            {
                CountingFill fill = new(new Color(30 + rowIndex * 20, 65 + column * 10, 105), this);
                Sources.Add(fill);
                IFillBrush source = fill;
                if (options.Mode == "background")
                {
                    MGCachedFillBrush cached = new(source);
                    Backgrounds.Add(cached);
                    source = cached;
                }
                MGTextBlock cell = new(Window, $"STATIC {rowIndex}:{column}", Color.White, 14, false)
                {
                    WrapText = false, PreferredWidth = 112, PreferredHeight = 34,
                    Padding = new(4), BackgroundBrush = new(source)
                };
                first ??= cell;
                Cells.Add(cell);
                row.TryAddChild(cell);
            }
            Probe probe = new(Window, this) { Padding = new(0) };
            probe.SetContent(row);
            Probes.Add(probe);
            MGElement content = probe;
            if (options.Mode == "panel")
            {
                MGRenderCache boundary = new(Window) { Padding = new(0), ClipToBounds = true };
                boundary.SetContent(probe);
                Boundaries.Add(boundary);
                content = boundary;
            }
            if (rowIndex == 0)
            {
                FirstHost.SetContent(content);
            }
            else
            {
                art.TryAddChild(content);
            }
        }
        StaticText = first!;
        Score = new(Window, "SCORE 001200", Color.Gold, 20, false) { WrapText = false, IsRenderVolatile = true };
        Place(root, Score, 8, 155, 245, 45);
        Button = new(Window) { PressedContentOffset = new(2, 2) };
        Button.SetContent(new MGTextBlock(Window, "LIVE INPUT", Color.White, 16, false));
        Button.OnLeftClicked += (_, _) => Clicks++;
        Place(root, Button, 270, 155, 160, 40);
        Scroll = new(Window, ScrollBarVisibility.Visible, ScrollBarVisibility.Disabled) { ClipToBounds = true };
        MGStackPanel rows = new(Window, Orientation.Vertical) { Spacing = 2 };
        for (int index = 0; index < 30; index++)
        {
            rows.TryAddChild(new MGTextBlock(Window, $"LIVE SCROLL ROW {index:00}", Color.LightCyan, 14, false)
                { PreferredHeight = 24, WrapText = false });
        }
        Scroll.SetContent(rows);
        Place(root, Scroll, 8, 220, 480, 130);
        Check(Desktop.RenderCaches.MaximumRetainedBytes == 33_554_432
            && Desktop.RenderCaches.MaximumEntries == 64 && Desktop.RenderCaches.MaximumTargetDimension == 4096,
            "Default resource policy changed.");
    }

    private static void Place(MGOverlayPanel panel, MGElement element, int left, int top, int width, int height)
    {
        element.PreferredWidth = width;
        element.PreferredHeight = height;
        element.HorizontalAlignment = HorizontalAlignment.Left;
        element.VerticalAlignment = VerticalAlignment.Top;
        panel.TryAddChild(element, new(left, top, 0, 0));
    }

    internal void Update(int frame)
    {
        int local = frame - Options.Warmup;
        int pulseFrame = -1;
        Clock = TimeSpan.FromTicks((frame + 1L) * TimeSpan.TicksPerSecond / 60);
        Mouse = new(2, 2, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        if (frame >= 8 && Options.Scenario is "hover-press" or "pulse")
        {
            Point warmCursor = Center(Options.Scenario == "pulse" ? Score : Button);
            Mouse = new(warmCursor.X, warmCursor.Y, 0, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released);
        }
        if (local >= 0)
        {
            switch (Options.Scenario)
            {
                case "hover-press":
                    Point cursor = Center(Button);
                    Mouse = new(cursor.X, cursor.Y, 0, local % 60 is >= 20 and < 30 ? ButtonState.Pressed : ButtonState.Released,
                        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    break;
                case "pulse":
                    pulseFrame = local % 120;
                    if (pulseFrame == 0)
                    {
                        Score.Text = $"SCORE {1201 + local / 120:000000}";
                    }
                    float pulse = pulseFrame < 24 ? MathF.Sin(MathF.PI * pulseFrame / 24) : 0;
                    Score.Foreground = new(Color.Lerp(Color.Gold, Color.White, pulse));
                    Score.RenderScale = pulse > 0 ? new(1 + .18f * pulse, 1 + .18f * pulse) : null;
                    Point scoreCursor = Center(Score);
                    Mouse = new(scoreCursor.X, scoreCursor.Y, 0, ButtonState.Released, ButtonState.Released,
                        ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    break;
                case "text":
                    StaticText.Text = local / 60 % 2 == 0 ? "TEXT A" : "TEXT B";
                    break;
                case "scrolling":
                    Scroll.VerticalOffset = local * 2 % Math.Max(1, (int)Scroll.MaxVerticalOffset);
                    break;
                case "resize-scale":
                    ApplyResize(local / 60 % 2 != 0);
                    break;
                case "continuous-animation":
                    foreach (MGTextBlock cell in Cells)
                    {
                        cell.IsRenderVolatile = true;
                    }
                    foreach (Probe probe in Probes)
                    {
                        probe.IsRenderVolatile = true;
                    }
                    Sources[0].SetColor(new Color(30 + local % 50, 65, 105));
                    Score.Foreground = new(new Color(150 + local % 100, 220, 120));
                    break;
                case "resource-soak":
                    ApplySoak(local);
                    break;
            }
        }
        PreviewUpdate?.Invoke(this, Clock);
        Desktop.Update();
        EndUpdate?.Invoke(this, EventArgs.Empty);
        float scoreScale = 1;
        Score.RenderScale?.TryGetScale(Score.VisualState, out scoreScale);
        Color scoreColor = Score.Foreground.NormalValue
            ?? throw new InvalidOperationException("The deterministic score foreground must be explicit.");
        State = new(frame, Clock.Ticks, Score.Text, StaticText.Text, pulseFrame, Button.VisualState.IsHovered,
            Button.VisualState.IsPressed, Clicks, Scroll.VerticalOffset, Window.WindowWidth, Window.WindowHeight,
            Scale, Visible, Enabled, Reparented, LastOperation, scoreScale, scoreColor.PackedValue,
            Sources[0].CurrentColor.PackedValue);
        CheckBudget();
    }

    private void ApplyResize(bool changed)
    {
        Window.WindowWidth = Options.Width - (changed ? 96 : 64);
        Window.WindowHeight = Options.Height - (changed ? 96 : 64);
        float scale = Options.Scale + (changed ? .125f : 0);
        if (Scale != scale)
        {
            Scale = scale;
            Desktop.SetUniformUIScale(scale);
        }
    }

    private void ApplySoak(int frame)
    {
        int operation = frame / 25 % 16;
        if (operation == LastOperation && frame % 25 != 0)
        {
            return;
        }
        LastOperation = operation;
        MGRenderCacheManager manager = Desktop.RenderCaches;
        manager.MaximumEntries = operation == 8 ? 2 : 64;
        manager.MaximumRetainedBytes = operation == 9 ? 80_000 : 33_554_432;
        Visible = operation != 1;
        Enabled = operation != 6;
        foreach (Probe probe in Probes)
        {
            probe.Visibility = Visible ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (MGRenderCache boundary in Boundaries)
        {
            boundary.IsCacheEnabled = Enabled;
        }
        foreach (MGCachedFillBrush background in Backgrounds)
        {
            background.IsCacheEnabled = Enabled;
        }
        ApplyResize(operation is 3 or 4);
        bool reparent = operation == 5;
        if (reparent != Reparented)
        {
            MGContentPresenter oldHost = Reparented ? AlternateHost : FirstHost;
            MGContentPresenter newHost = reparent ? AlternateHost : FirstHost;
            MGElement content = oldHost.Content;
            oldHost.SetContent((MGElement)null!);
            newHost.SetContent(content);
            Reparented = reparent;
        }
        if (operation == 10)
        {
            manager.Clear();
            CheckEmpty();
        }
        if (operation == 11)
        {
            StaticText.Text = frame / 400 % 2 == 0 ? "GEN A" : "GEN B";
        }
        CheckBudget();
    }

    internal void AfterPrepare(int frame)
    {
        int local = frame - Options.Warmup;
        if (Options.Scenario == "resource-soak" && local >= 0 && local % 400 == 300)
        {
            long bytes = Desktop.RenderCaches.RetainedBytes;
            int entries = Desktop.RenderCaches.EntryCount;
            if (Options.Mode != "normal")
            {
                Check(bytes > 0 && entries > 0, "Late mutation did not start with established native targets.");
            }
            LatePaintBaseline = Probes[0].Paints;
            LateSourcePaintBaseline = Sources[0].DirectPaints;
            StaticText.Text = StaticText.Text == "LATE A" ? "LATE B" : "LATE A";
            Sources[0].SetColor(Sources[0].CurrentColor == Color.Magenta ? Color.Lime : Color.Magenta);
            State = State with { StaticText = StaticText.Text, SourceColor = Sources[0].CurrentColor.PackedValue };
            Check(Desktop.RenderCaches.EntryCount == entries && Desktop.RenderCaches.RetainedBytes == bytes,
                "Late mutation disposed a target outside the next safe preparation point.");
            RetirementChecks++;
        }
        CheckBudget();
    }

    internal void AfterPaint(int frame)
    {
        int local = frame - Options.Warmup;
        if (Options.Scenario == "resource-soak" && local >= 0 && local % 400 == 300)
        {
            Check(Probes[0].Paints == LatePaintBaseline + 1, "Late generation sampled stale pixels instead of direct paint.");
            Check(Sources[0].DirectPaints == LateSourcePaintBaseline + 1,
                "Late source mutation sampled a stale cached fill instead of painting the changed source.");
            LateMutationChecks++;
        }
        bool invariantInterval = Options.Scenario is "idle-hud" or "pulse" or "hover-press" or "scrolling";
        if (invariantInterval && local >= 0)
        {
            Check(WarmEstablished, "Measured static interval began without establishing its warm baseline.");
            CheckIdle();
        }
        int interval = Options.Scenario == "resource-soak" && local >= 0 ? local % 25 : local;
        if (Options.Scenario == "resource-soak" && LastOperation is 0 or 15 && interval == 8)
        {
            IdleDiscoveryBaseline = DiscoveryVisits;
            IdleReadBaseline = SourceReads;
            IdleBuildBaseline = Desktop.RenderCaches.Statistics.Builds;
        }
        if (Options.Scenario == "resource-soak" && LastOperation is 0 or 15 && interval > 8)
        {
            CheckIdle();
        }
        CheckBudget();
    }

    internal void MarkWarm()
    {
        MGRenderCacheManager manager = Desktop.RenderCaches;
        if (Options.Scenario is "hover-press" or "pulse")
        {
            MGElement hoverTarget = Options.Scenario == "pulse" ? Score : Button;
            Check(hoverTarget.VisualState.IsHovered, "The real input target did not establish hover during warmup.");
        }
        if (Options.Mode != "normal")
        {
            Check(manager.Statistics.Builds > 0 && manager.Statistics.Hits > 0
                && manager.EntryCount > 0 && manager.RetainedBytes > 0,
                "Known eligible fixture did not establish positive native builds, hits, entries and bytes.");
        }
        IdleDiscoveryBaseline = DiscoveryVisits;
        IdleReadBaseline = SourceReads;
        IdleBuildBaseline = manager.Statistics.Builds;
        WarmEstablished = true;
    }

    private void CheckIdle()
    {
        Check(Desktop.RenderCaches.Statistics.Builds == IdleBuildBaseline, "An unchanged static sibling rebuilt.");
        Check(DiscoveryVisits == IdleDiscoveryBaseline && SourceReads == IdleReadBaseline,
            "An idle cache rediscovered its subtree or source graph.");
        IdleChecks++;
    }

    internal bool TryGetLatePixel(int frame, out Point samplePosition, out Color expected)
    {
        samplePosition = default;
        expected = default;
        int local = frame - Options.Warmup;
        if (Options.Scenario != "resource-soak" || local < 0 || local % 400 != 300)
        {
            return false;
        }
        samplePosition = StaticText.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
            new Point(StaticText.LayoutBounds.Left + 1, StaticText.LayoutBounds.Top + 1));
        expected = Sources[0].CurrentColor;
        return true;
    }

    internal void CheckBudget()
    {
        MGRenderCacheManager manager = Desktop.RenderCaches;
        Check(manager.RetainedBytes >= 0 && manager.RetainedBytes <= manager.MaximumRetainedBytes,
            "Allocated, retired or in-flight target bytes exceeded the configured budget.");
        Check(manager.EntryCount >= 0 && manager.EntryCount <= manager.MaximumEntries,
            "Allocated, retired or in-flight entries exceeded the configured budget.");
        Check(manager.Statistics.Allocations - manager.Statistics.Disposals == manager.EntryCount,
            "Live entries differ from actual allocations minus disposals.");
        MaximumBytes = Math.Max(MaximumBytes, manager.RetainedBytes);
        MaximumEntries = Math.Max(MaximumEntries, manager.EntryCount);
    }

    internal void CheckEmpty()
    {
        MGRenderCacheManager manager = Desktop.RenderCaches;
        Check(manager.RetainedBytes == 0 && manager.EntryCount == 0 && manager.PendingCount == 0
            && manager.Statistics.Allocations == manager.Statistics.Disposals, "Clear/teardown retained resource work.");
    }

    private void CheckInFlight()
    {
        CheckBudget();
        Check(GraphicsDevice.RenderTargetCount == 1, "Preparation did not bind one actual native candidate.");
        GraphicsDevice.GetRenderTargets(CaptureTarget);
        Check(CaptureTarget[0].RenderTarget is RenderTarget2D, "The capture target is not a native RenderTarget2D.");
        RenderTarget2D target = (RenderTarget2D)CaptureTarget[0].RenderTarget;
        Check(Desktop.RenderCaches.EntryCount > 0
            && Desktop.RenderCaches.RetainedBytes >= checked((long)target.Width * target.Height * 4),
            "The bound in-flight target was not charged.");
        InFlightChecks++;
    }

    private Point Center(MGElement element) => element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, element.LayoutBounds.Center);
    public Rectangle GetBounds() => new(0, 0, Options.Width, Options.Height);
    public MouseState GetMouseState() => Mouse;
    public KeyboardState GetKeyboardState() => default;
    public object? GetService(Type type) => Game.Services.GetService(type);
    public void Dispose()
    {
        Desktop.DisposeRenderCaches();
        CheckEmpty();
        Renderer.SpriteBatch.Dispose();
        Renderer.PrimitiveBatch.Dispose();
        Renderer.Content.Dispose();
        Font.Dispose();
    }
    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Probe(MGWindow window, StabilityFixture fixture) : MGContentPresenter(window)
    {
        internal int Captures;
        internal int Paints;
        internal long Discoveries;
        protected override bool SupportsRenderCaching => true;
        protected override IEnumerable<object> EnumerateControlRenderSources()
        {
            Discoveries++;
            foreach (object source in base.EnumerateControlRenderSources())
            {
                yield return source;
            }
        }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            if (args.DT.IsCacheCapture)
            {
                Captures++;
                fixture.CheckInFlight();
            }
            else
            {
                Paints++;
            }
            base.DrawSelf(args, bounds);
        }
    }

    private sealed class CountingFill(Color color, StabilityFixture fixture) : IFillBrush, IMGRenderCacheSource
    {
        private Color Fill = color;
        private ulong Revision = 1;
        internal long Reads;
        internal int DirectPaints;
        internal Color CurrentColor => Fill;
        public ulong VisualRevision { get { Reads++; return Revision; } }
        public bool IsRenderVolatile { get { Reads++; return false; } }
        public bool SupportsRenderCaching { get { Reads++; return true; } }
        public event EventHandler? VisualInvalidated;
        internal void SetColor(Color value)
        {
            if (Fill != value)
            {
                Fill = value;
                Revision++;
                VisualInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds)
        {
            if (args.DT.IsCacheCapture)
            {
                fixture.CheckInFlight();
            }
            else
            {
                DirectPaints++;
            }
            new MGSolidFillBrush(Fill).Draw(args, element, bounds);
        }
        public IFillBrush Copy() => new CountingFill(Fill, fixture);
    }
}
