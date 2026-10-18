using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace MGUI.RenderCacheTestHost;

// A genuine opt-in paint producer exercises the preparation protocol before public brush/panel producers land.
internal sealed class PreparationTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;
    internal PreparationTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using PreparationTests game = new();
        game.Run();
        Check(game.Completed, "Preparation checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; native backbuffer RGBA readback; renderer recorded by test-native.py.");
        return 0;
    }
    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 50, 240, 180) { WindowStyle = WindowStyle.None, Padding = new(0) };
        Probe probe = new(window);
        window.SetContent(probe);
        desktop.Windows.Add(window);
        void Update() => fixture.Update(++Frame);
        Color[] Draw(bool cache, int world = 7, float opacity = 1, DrawSettings? settings = null, Rectangle? clip = null)
        {
            probe.Cache = cache;
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13, 29, 41));
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                for (int i = 0; i < 20; i++)
                {
                    transaction.FillRectangle(Vector2.Zero, new Rectangle((i * 41 + world) % 640, i * 21, 31, 17), Color.Goldenrod);
                }
            }
            var before = GraphicsDevice.Metrics.TargetCount;
            GraphicsDevice.ScissorRectangle = clip ?? GraphicsDevice.Viewport.Bounds;
            desktop.Draw(opacity, settings);
            Check(GraphicsDevice.Metrics.TargetCount == before, "Final UI switched a render target.");
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        void Queue()
        {
            Update();
            Draw(true);
            Check(desktop.RenderCaches.PendingCount == 1, "Direct paint did not queue exactly one observation.");
            Update();
        }
        for (int settle = 0; settle < 4; settle++) { Update(); Draw(false); }
        Update();
        Color[] reference = Draw(false);
        Draw(true);
        Check(desktop.RenderCaches.Statistics.Allocations == 0, "Final Draw allocated.");
        Update();
        GraphicsDevice.BlendState = BlendState.Additive;
        GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        desktop.PrepareRenderCaches();
        Check(GraphicsDevice.BlendState == BlendState.Additive && GraphicsDevice.RasterizerState == RasterizerState.CullClockwise
            && GraphicsDevice.DepthStencilState == DepthStencilState.DepthRead && GraphicsDevice.SamplerStates[0] == SamplerState.LinearWrap,
            "Primitive-first capture did not restore poisoned host state.");
        Check(desktop.RenderCaches.Statistics.Builds == 1, "First preparation did not build.");
        int captures = probe.Operation.Captures;
        desktop.PrepareRenderCaches();
        Check(probe.Operation.Captures == captures, "Duplicate preparation painted twice.");
        Color[] cached = Draw(true);
        Equal(reference, cached, "cold and warm pixels");
        Check(desktop.RenderCaches.Statistics.Hits == 1, "Warm cache was not used.");
        Results["scene-parity-and-no-final-target-switch"] = true;
        Results["primitive-first-poisoned-host-state-parity"] = true;
        Results["duplicate-preparation"] = true;
        Color[] moving = Draw(true, 19);
        Color[] movingReference = Draw(false, 19);
        Equal(movingReference, moving, "moving scene");
        Queue(); desktop.PrepareRenderCaches(); Draw(true);
        Update();
        long hits = desktop.RenderCaches.Statistics.Hits;
        Equal(Draw(true, 25), Draw(false, 25), "omitted prepare");
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Omitted Prepare used an old target.");
        Results["omitted-preparation-direct"] = true;

        Queue();
        probe.InvalidateVisual();
        captures = probe.Operation.Captures;
        desktop.PrepareRenderCaches();
        Check(probe.Operation.Captures == captures, "Stale observation captured before direct refresh.");
        Queue(); desktop.PrepareRenderCaches();
        probe.InvalidateVisual();
        hits = desktop.RenderCaches.Statistics.Hits;
        Draw(true);
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Late invalidation used stale pixels.");
        Results["before-and-after-preparation-invalidation"] = true;

        foreach (string context in new[] { "opacity", "partial-clip", "transform", "sampler" })
        {
            Queue(); desktop.PrepareRenderCaches();
            hits = desktop.RenderCaches.Statistics.Hits;
            float opacity = context == "opacity" ? 0.5f : 1;
            Rectangle? clip = context == "partial-clip" ? new Rectangle(50, 60, 25, 25) : null;
            DrawSettings settings = context == "transform" ? new(Matrix.CreateScale(1.25f))
                : context == "sampler" ? DrawSettings.Default with { SamplerType = SamplerType.LinearClamp } : DrawSettings.Default;
            Equal(Draw(true, 7, opacity, settings, clip), Draw(false, 7, opacity, settings, clip), context + " fallback");
            Check(desktop.RenderCaches.Statistics.Hits == hits && desktop.RenderCaches.PendingCount == 0, "Unsupported context reused/queued.");
        }
        Results["opacity-clip-transform-sampler-fallback"] = true;
        using Texture2D callerTexture = new(GraphicsDevice, 1, 1);
        callerTexture.SetData(new[] { Color.Purple });
        using IndexBuffer callerIndices = new(GraphicsDevice, IndexElementSize.SixteenBits, 3, BufferUsage.WriteOnly);
        callerIndices.SetData(new short[] { 0, 1, 2 });
        foreach (string action in new[] { "invalidate", "clear", "throw", "reenter" })
        {
            Queue();
            long builds = desktop.RenderCaches.Statistics.Builds;
            probe.Operation.DuringCapture = () =>
            {
                if (action == "invalidate") { probe.InvalidateVisual(); }
                if (action == "clear") { desktop.RenderCaches.Clear(); }
                if (action == "throw") { throw new TestCaptureException(); }
                if (action == "reenter") { desktop.PrepareRenderCaches(); }
            };
            Viewport viewport = GraphicsDevice.Viewport;
            Rectangle scissor = new(3, 4, 600, 450);
            GraphicsDevice.ScissorRectangle = scissor;
            GraphicsDevice.BlendState = BlendState.Additive;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            GraphicsDevice.SamplerStates[1] = SamplerState.PointWrap;
            GraphicsDevice.Textures[0] = callerTexture;
            GraphicsDevice.Indices = callerIndices;
            GraphicsDevice.BlendFactor = Color.Yellow;
            var rasterizer = GraphicsDevice.RasterizerState;
            bool threw = false;
            try { desktop.PrepareRenderCaches(); }
            catch (TestCaptureException) { threw = true; }
            Check(threw == (action == "throw"), "Capture exception contract changed.");
            Check(GraphicsDevice.GetRenderTargets().Length == 0 && GraphicsDevice.Viewport.Bounds == viewport.Bounds
                && GraphicsDevice.ScissorRectangle == scissor && GraphicsDevice.BlendState == BlendState.Additive
                && GraphicsDevice.DepthStencilState == DepthStencilState.DepthRead
                && GraphicsDevice.SamplerStates[0] == SamplerState.LinearWrap && GraphicsDevice.SamplerStates[1] == SamplerState.PointWrap
                && ReferenceEquals(GraphicsDevice.Textures[0], callerTexture) && ReferenceEquals(GraphicsDevice.Indices, callerIndices)
                && GraphicsDevice.BlendFactor == Color.Yellow && GraphicsDevice.RasterizerState == rasterizer,
                "Device state not restored after " + action);
            Check(!DrawTransaction.HasActiveTransactions(GraphicsDevice), "Capture leaked a transaction.");
            Check(desktop.RenderCaches.Statistics.Builds == builds + (action == "reenter" ? 1 : 0), "Stale/failed capture published.");
            if (action != "reenter") { Check(desktop.RenderCaches.EntryCount == 0, "Rejected capture retained a target."); }
            probe.Operation.DuringCapture = null;
            GraphicsDevice.ScissorRectangle = viewport.Bounds;
            Results["capture-" + action + "-state-restoration"] = true;
        }
        Queue();
        Viewport fullViewport = GraphicsDevice.Viewport;
        captures = probe.Operation.Captures;
        GraphicsDevice.Viewport = new Viewport(5, 6, 600, 440);
        desktop.PrepareRenderCaches();
        Check(probe.Operation.Captures == captures, "Changed viewport captured an old observation.");
        GraphicsDevice.Viewport = fullViewport;
        Queue(); desktop.PrepareRenderCaches();
        hits = desktop.RenderCaches.Statistics.Hits;
        GraphicsDevice.Viewport = new Viewport(5, 6, 600, 440);
        Equal(Draw(true), Draw(false), "changed viewport fallback");
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Changed viewport reused a cache.");
        GraphicsDevice.Viewport = fullViewport;
        Results["viewport-change-before-prepare-and-hit"] = true;

        Queue();
        using (DrawTransaction external = new(desktop.Renderer, DrawSettings.Default, true))
        {
            desktop.PrepareRenderCaches();
            Check(!desktop.CanUseRenderCaches, "Active deferred external transaction was accepted.");
            external.FillRectangle(Vector2.Zero, new Rectangle(0, 0, 2, 2), Color.Red);
        }
        Results["external-transaction-bypass"] = true;

        foreach (int count in new[] { 1, 2 })
        {
            Queue();
            using RenderTarget2D first = new(GraphicsDevice, 32, 32, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            using RenderTarget2D second = new(GraphicsDevice, 32, 32, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            GraphicsDevice.SetRenderTargets(count == 1 ? new[] { new RenderTargetBinding(first) }
                : new[] { new RenderTargetBinding(first), new RenderTargetBinding(second) });
            GraphicsDevice.Clear(Color.CornflowerBlue);
            long switches = desktop.RenderCaches.Statistics.TargetSwitches;
            desktop.PrepareRenderCaches();
            Check(GraphicsDevice.GetRenderTargets().Length == count && ReferenceEquals(GraphicsDevice.GetRenderTargets()[0].RenderTarget, first)
                && desktop.RenderCaches.Statistics.TargetSwitches == switches && !desktop.CanUseRenderCaches, "Host target changed.");
            GraphicsDevice.SetRenderTarget(null);
            Color[] targetPixels = new Color[32 * 32]; first.GetData(targetPixels);
            Check(targetPixels.All(x => x == Color.CornflowerBlue), "Host target pixels destroyed.");
        }
        Results["single-and-multiple-host-target-bypass"] = true;
        Queue();
        probe.Operation.ThrowDirect = true;
        bool directThrew = false;
        try { Draw(true); }
        catch (TestCaptureException) { directThrew = true; }
        probe.Operation.ThrowDirect = false;
        Check(directThrew && desktop.RenderCaches.PendingCount == 0, "Failed direct paint queued an observation.");
        Results["failed-direct-paint-does-not-queue"] = true;

        int hooks = 0;
        EventHandler<MGElement.MGElementDrawEventArgs> hook = (_, _) => hooks++;
        probe.OnBeginDraw += hook;
        bool clipped = probe.RecentDrawWasClipped;
        using (RenderTarget2D capture = new(GraphicsDevice, 640, 480))
        {
            GraphicsDevice.SetRenderTarget(capture);
            GraphicsDevice.Clear(Color.Transparent);
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                transaction.ConfigureCacheCapture(Point.Zero);
                GraphicsDevice.ScissorRectangle = new(0, 0, 1, 1);
                probe.Draw(new(new(TimeSpan.Zero, transaction, 1), probe.VisualState, Point.Zero));
            }
            GraphicsDevice.SetRenderTarget(null);
        }
        probe.OnBeginDraw -= hook;
        Check(hooks == 0 && probe.RecentDrawWasClipped == clipped, "Capture invoked public hooks or changed clipping bookkeeping.");
        Results["capture-suppresses-hooks-and-bookkeeping"] = true;

        Queue();
        window.Desktop.Windows.Remove(window);
        captures = probe.Operation.Captures;
        desktop.PrepareRenderCaches();
        Check(probe.Operation.Captures == captures, "Detached root captured.");
        desktop.Windows.Add(window);
        Queue();
        probe.Operation.DuringCapture = () => desktop.DisposeRenderCaches();
        desktop.PrepareRenderCaches();
        Check(desktop.RenderCaches.IsDisposed && desktop.RenderCaches.EntryCount == 0 && !desktop.CanUseRenderCaches, "Reentrant dispose failed.");
        Draw(true);
        Results["root-detach-and-reentrant-disposal"] = true;
        Completed = true;
    }
    protected override void Update(GameTime gameTime) { Exit(); }
    protected override void Draw(GameTime gameTime) { }
    private static void Equal(Color[] a, Color[] b, string name)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) { throw new InvalidOperationException(name + " pixel " + i + ": " + a[i] + " / " + b[i]); }
        }
    }
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    private sealed class TestCaptureException : Exception { }
    private sealed class Probe : MGRectangle
    {
        internal bool Cache = true;
        internal ProbeOperation Operation;
        internal Probe(MGWindow window) : base(window, 180, 110, Color.Transparent, 0, Color.Transparent)
        {
            Operation = new(window.Desktop, this);
        }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            if (Cache && Operation.TryDraw(args, bounds, 1)) { return; }
            var observation = Operation.BeginDirect(args, bounds, 1, Cache);
            Operation.PaintDirect(args, bounds);
            Operation.CompleteDirect(observation);
        }
    }
    private sealed class ProbeOperation : MGRenderCacheOperation
    {
        internal int Captures;
        internal Action? DuringCapture;
        internal bool ThrowDirect;
        internal ProbeOperation(MGDesktop desktop, Probe owner) : base(desktop, owner) { }
        protected override bool IsSourceCurrent(MGElement owner, MGRenderCacheKey key) => ((Probe)owner).Cache;
        internal void PaintDirect(ElementDrawArgs args, Rectangle bounds)
        {
            if (ThrowDirect) { throw new TestCaptureException(); }
            args.DT.FillTriangle(args.Offset.ToVector2(), new(bounds.Left, bounds.Top), Color.Teal,
                new(bounds.Right, bounds.Top), Color.Teal, new(bounds.Left, bounds.Bottom), Color.Teal);
            Rectangle clip = new(bounds.X + 11 + args.Offset.X, bounds.Y + 9 + args.Offset.Y, 23, 19);
            using (args.DT.SetClipTargetTemporary(args.DT.MapScreenClip(clip), true))
            {
                args.DT.FillRectangle(args.Offset.ToVector2(), bounds, Color.OrangeRed);
            }
        }
        protected override void Paint(MGElement owner, ElementDrawArgs args, Rectangle bounds)
        {
            Captures++;
            PaintDirect(args, bounds);
            DuringCapture?.Invoke();
        }
    }
}
