using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

internal sealed class NormalPathTests : Game
{
    private readonly List<object> Comparisons = new();
    private readonly List<object> Idle = new();
    private bool Completed;
    private int StableBookkeepingFrames;
    private NormalPathTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using NormalPathTests game = new();
        game.Run();
        Check(game.Completed, "Normal path lane did not complete.");
        return 0;
    }
    protected override void LoadContent()
    {
        Check(GraphicsDevice.PresentationParameters.BackBufferWidth == 640
            && GraphicsDevice.PresentationParameters.BackBufferHeight == 480, "Normal guard backbuffer dimensions differ.");
        foreach (float scale in new[] { 1f, 1.5f })
        {
            foreach (string scenario in Options.Scenarios)
            {
                using BenchmarkFixture normal = new(this, new(scenario, 640, 480, scale, 1729, 0, 1, "", ""));
                using BenchmarkFixture disabled = new(this, new(scenario, 640, 480, scale, 1729, 0, 1, "", "", "disabled"));
                for (int frame = 0; frame < 12; frame++)
                {
                    normal.Update(frame); disabled.Update(frame);
                    Check(normal.State == disabled.State, "Normal/disabled Update state differs.");
                    var first = Paint(normal);
                    var second = Paint(disabled);
                    Check(first.Counters == second.Counters, scenario + " normal/disabled batching counters differ.");
                    Check(first.Pixels.SequenceEqual(second.Pixels), scenario + " normal/disabled RGBA differs at " + scale);
                    Comparisons.Add(new { scenario, scale, frame, exactRgba = true, counters = first.Counters });
                }
            }
        }
        foreach (bool disabled in new[] { false, true })
        {
            foreach (int count in new[] { 10, 100, 1000 })
            {
                using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
                MGWindow window = fixture.Desktop.Windows[0];
                MGStackPanel tree = new(window, Orientation.Vertical) { Spacing = 0 };
                CountingBrush source = new();
                Probe[] nodes = Enumerable.Range(0, count).Select(_ => new Probe(window, source)).ToArray();
                foreach (Probe node in nodes) { tree.TryAddChild(node); }
                if (disabled)
                {
                    MGRenderCache boundary = new(window) { IsCacheEnabled = false, Padding = new(0) };
                    boundary.SetContent(tree); window.SetContent(boundary);
                }
                else { window.SetContent(tree); }
                for (int frame = 0; frame < 8; frame++) { fixture.Update(frame); Paint(fixture); }
                foreach (Probe node in nodes) { node.Discoveries = 0; }
                source.Reads = source.Changes = 0;
                for (int frame = 8; frame < 28; frame++) { fixture.Update(frame); Paint(fixture); }
                Check(nodes.All(node => node.Discoveries == 0 && !node.IsRenderSourceSubtreeTrackingEnabled
                    && node.RenderContentObserverSlots == 0), "Unused cache discovered/subscribed to an idle subtree.");
                Check(source.Reads == 0 && source.Changes == 0 && source.Active == 0, "Unused cache observed sources.");
                Idle.Add(new { disabled, count, frames = 20, discoveryVisits = 0, sourceReads = 0,
                    observerSlots = 0, subscriptionChanges = 0 });
            }
        }
        long emptyPreparationBytes = 0;
        foreach (string mode in new[] { "normal", "disabled" })
        {
            using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", "", mode));
            for (int frame = 0; frame < 8; frame++) { fixture.Update(frame); Paint(fixture); }
            MGElement root = fixture.Desktop.Windows[0].Content;
            var snapshot = root.GetRenderRevisionSnapshot();
            int notifications = 0;
            root.RenderInvalidated += (_, _, _) => notifications++;
            for (int frame = 8; frame < 58; frame++)
            {
                fixture.Update(frame);
                long beforePrepare = GC.GetAllocatedBytesForCurrentThread();
                fixture.Desktop.PrepareRenderCaches();
                emptyPreparationBytes += GC.GetAllocatedBytesForCurrentThread() - beforePrepare;
                Paint(fixture);
                Check(root.IsRenderRevisionCurrent(snapshot), "Normal paint bookkeeping invalidated idle pixels.");
                StableBookkeepingFrames++;
            }
            Check(notifications == 0, "Normal paint created revision notification loops.");
        }
        Check(emptyPreparationBytes == 0, "Unused preparation allocates every frame: " + emptyPreparationBytes);
        using BenchmarkFixture ordinary = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        using BenchmarkFixture disabledWrapper = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", "", "disabled"));
        MGWindow ordinaryWindow = ordinary.Desktop.Windows[0];
        MGElement ordinaryRoot = ordinaryWindow.Content;
        ordinaryWindow.SetContent((MGElement)null!);
        MGContentPresenter presenter = new(ordinaryWindow) { Padding = new(0) };
        presenter.SetContent(ordinaryRoot); ordinaryWindow.SetContent(presenter);
        for (int frame = 0; frame < 20; frame++)
        { ordinary.Update(frame); disabledWrapper.Update(frame); Paint(ordinary); Paint(disabledWrapper); }
        var ordinaryPaint = Paint(ordinary);
        var disabledPaint = Paint(disabledWrapper);
        Check(ordinaryPaint.Pixels.SequenceEqual(disabledPaint.Pixels) && ordinaryPaint.Counters == disabledPaint.Counters,
            "Generic-wrapper allocation comparator differs in geometry or batching.");
        long[] DrawBytes(BenchmarkFixture fixture)
        {
            long[] samples = new long[100];
            for (int frame = 0; frame < samples.Length; frame++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                fixture.Desktop.Draw();
                samples[frame] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            return samples;
        }
        long[] ordinaryDrawBytes = DrawBytes(ordinary);
        long[] disabledDrawBytes = DrawBytes(disabledWrapper);

        // Native batching can allocate occasionally; a per-draw cache cost raises the steady floor.
        Check(ordinaryDrawBytes.Min() == disabledDrawBytes.Min(),
            "Disabled caching adds steady allocation beyond ordinary wrapper painting.");
        int inputFrames = 0;
        foreach (float scale in new[] { 1f, 1.5f })
        {
            using LiveSiblingFixture normalInput = new(this, scale, 1, cached: false);
            using LiveSiblingFixture disabledInput = new(this, scale, 1, cached: false);
            MGElement inputRoot = disabledInput.Window.Content;
            disabledInput.Window.SetContent((MGElement)null!);
            MGRenderCache inputBoundary = new(disabledInput.Window) { IsCacheEnabled = false, Padding = new(0) };
            inputBoundary.SetContent(inputRoot); disabledInput.Window.SetContent(inputBoundary);
            void Step(Point cursor, bool pressed = false, KeyboardState keyboard = default)
            {
                MouseState mouse = new(cursor.X, cursor.Y, 0, pressed ? ButtonState.Pressed : ButtonState.Released,
                    ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                normalInput.Mouse = disabledInput.Mouse = mouse;
                normalInput.Keyboard = disabledInput.Keyboard = keyboard;
                normalInput.Tick(); disabledInput.Tick();
                Check(normalInput.Desktop.InputTracker.Mouse.CurrentState == disabledInput.Desktop.InputTracker.Mouse.CurrentState
                    && normalInput.Desktop.InputTracker.Keyboard.CurrentState == disabledInput.Desktop.InputTracker.Keyboard.CurrentState,
                    "Normal/disabled host input diverged.");
                Check(normalInput.Button.VisualState == disabledInput.Button.VisualState && normalInput.TextBox.Text == disabledInput.TextBox.Text
                    && normalInput.Events.SequenceEqual(disabledInput.Events), "Normal/disabled interaction state diverged.");
                Check(normalInput.Paint().SequenceEqual(disabledInput.Paint()), "Normal/disabled input pixels diverged.");
                Check(normalInput.Desktop.RenderCaches.Statistics.Allocations == 0 && disabledInput.Desktop.RenderCaches.Statistics.Allocations == 0,
                    "Normal/disabled input allocated cache targets.");
                inputFrames++;
            }
            for (int frame = 0; frame < 5; frame++) { Step(new(2, 2)); }
            Point button = normalInput.Center(normalInput.Button);
            Step(button); Check(normalInput.Button.VisualState.IsHovered, "Normal input did not hover button.");
            Step(button, true); Check(normalInput.Button.VisualState.IsPressed, "Normal input did not press button.");
            Step(button); Check(normalInput.Events.Count(value => value == "button") == 1, "Normal input click was lost.");
            normalInput.Button.IsEnabled = disabledInput.Button.IsEnabled = false;
            Step(button, true); Step(button);
            Check(normalInput.Events.Count(value => value == "button") == 1 && normalInput.Button.VisualState.IsDisabled, "Disabled input received click.");
            Point text = normalInput.Center(normalInput.TextBox);
            Step(text); Step(text, true); Step(text);
            Check(normalInput.Desktop.FocusedKeyboardHandler == normalInput.TextBox
                && disabledInput.Desktop.FocusedKeyboardHandler == disabledInput.TextBox, "Normal/disabled keyboard focus diverged.");
            Step(text, keyboard: new(Keys.A)); Step(text); Step(text, keyboard: new(Keys.B)); Step(text);
            Check(normalInput.TextBox.Text == "ab", "Normal/disabled focused keyboard input was lost.");
        }
        using BenchmarkFixture profile = new(this, new("animated-score", 640, 480, 1, 1729, 0, 1, "", ""));
        MGElement leaf = ((MGStackPanel)((MGStackPanel)profile.Desktop.Windows[0].Content).Children[0]).Children[1];
        for (int iteration = 0; iteration < 1000; iteration++) { leaf.InvalidateRenderState(MGRenderInvalidationKind.Visual); }
        long[] revisionAllocationBursts = new long[5];
        for (int burst = 0; burst < revisionAllocationBursts.Length; burst++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 10000; iteration++) { leaf.InvalidateRenderState(MGRenderInvalidationKind.Visual); }
            revisionAllocationBursts[burst] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long revisionBytes = revisionAllocationBursts.Min();
        string result = JsonSerializer.Serialize(new { exactComparisons = Comparisons.Count,
            comparisons = Comparisons, idle = Idle, stableBookkeepingFrames = StableBookkeepingFrames, emptyPreparationBytes, inputFrames, revisionNotifications = 10000, revisionBytes, revisionAllocationBursts, ordinaryWrapperDrawBytes = ordinaryDrawBytes, disabledWrapperDrawBytes = disabledDrawBytes, allocationFrames = 100,
            backend = "MonoGame DesktopGL", configuration = "Release", width = 640, height = 480,
            drawBaseArgsBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<MGUI.Shared.Rendering.DrawBaseArgs>(),
            elementDrawArgsBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<ElementDrawArgs>() });
        string report = Path.Combine(Path.GetTempPath(), "mgui-normal-guards-" + Environment.ProcessId + ".json");
        File.WriteAllText(report, result);
        Console.WriteLine("REPORT:" + report);
        Console.WriteLine("RESULT:" + result);
        Completed = true;
        Exit();
    }
    private (Color[] Pixels, Counters Counters) Paint(BenchmarkFixture fixture)
    {
        long switches = GraphicsDevice.Metrics.TargetCount;
        fixture.Desktop.PrepareRenderCaches();
        Check(GraphicsDevice.Metrics.TargetCount == switches, "Unused Prepare switched targets.");
        GraphicsDevice.Clear(new Color(18, 25, 39));
        GraphicsMetrics before = GraphicsDevice.Metrics;
        fixture.Desktop.Draw();
        Counters counters = Counters.From(GraphicsDevice.Metrics - before);
        Check(counters.TargetSwitches == 0 && fixture.GetCacheCounters() == default, "Unused cache performed resource work.");
        Color[] pixels = new Color[640 * 480];
        GraphicsDevice.GetBackBufferData(pixels);
        return (pixels, counters);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private sealed class Probe : MGRectangle
    {
        private readonly CountingBrush Source;
        internal int Discoveries;
        internal Probe(MGWindow window, CountingBrush source) : base(window, 1, 1, Color.Transparent, 0, Color.White)
        { Source = source; }
        protected override IEnumerable<object> EnumerateControlRenderSources()
        {
            Discoveries++;
            yield return Source;
        }
    }
    private sealed class CountingBrush : IFillBrush, IMGRenderCacheSource
    {
        internal int Reads, Changes, Active;
        public ulong VisualRevision { get { Reads++; return 1; } }
        public bool IsRenderVolatile { get { Reads++; return false; } }
        public bool SupportsRenderCaching { get { Reads++; return true; } }
        public event EventHandler VisualInvalidated
        {
            add { Changes++; Active++; }
            remove { Changes++; Active--; }
        }
        public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds) { }
        public IFillBrush Copy() => new CountingBrush();
    }
}
