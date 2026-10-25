using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace MGUI.RenderCacheTestHost;

internal sealed class CachedBrushTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;
    internal CachedBrushTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using CachedBrushTests game = new();
        game.Run();
        Check(game.Completed, "Cached brush checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; native RGBA readback; renderer recorded by test-native.py.");
        return 0;
    }
    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        var source = new Source();
        var cached = new MGCachedFillBrush(source);
        MGRectangle[] owners = new MGRectangle[2];
        for (int i = 0; i < owners.Length; i++)
        {
            MGWindow window = new(desktop, 30 + i * 230, 40 + i * 90, 160, 120) { WindowStyle = WindowStyle.None, Padding = new(0) };
            owners[i] = new(window, 120, 80, Color.Transparent, 0, Color.Transparent)
            {
                Name = "owner" + i, BackgroundBrush = new(cached), OverlayBrush = cached
            };
            window.SetContent(owners[i]);
            desktop.Windows.Add(window);
        }
        void Update() => fixture.Update(++Frame);
        Color[] Draw(bool enabled = true, float opacity = 1, Rectangle? clip = null)
        {
            cached.IsCacheEnabled = enabled;
            GraphicsDevice.ScissorRectangle = clip ?? GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13, 29, 41));
            var switches = GraphicsDevice.Metrics.TargetCount;
            long allocations = desktop.RenderCaches.Statistics.Allocations;
            desktop.Draw(opacity);
            Check(GraphicsDevice.Metrics.TargetCount == switches && desktop.RenderCaches.Statistics.Allocations == allocations,
                "Final brush Draw switched targets or allocated.");
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        void Queue()
        {
            Update();
            Draw();
            Check(desktop.RenderCaches.PendingCount == 4, "Shared brush did not preserve two owners and two sites.");
            Update();
            desktop.PrepareRenderCaches();
        }
        for (int settle = 0; settle < 4; settle++) { Update(); Draw(false); }
        var reference = Draw(false);
        Queue();
        Check(desktop.RenderCaches.EntryCount == 4, "Owner/site targets were merged.");
        long hits = desktop.RenderCaches.Statistics.Hits;
        int calls = source.Calls.Count;
        Equal(reference, Draw(), "shared owner/site cache parity");
        Check(desktop.RenderCaches.Statistics.Hits == hits + 4 && source.Calls.Count == calls, "Warm draw did not use all four entries.");
        Results["per-owner-per-site-native-parity"] = true;
        Results["no-final-target-switch-or-allocation"] = true;
        Check(source.Calls.Where(x => x.Capture).All(c => source.Calls.Any(d => !d.Capture && d.Owner == c.Owner
            && d.Bounds == c.Bounds && d.Offset == c.Offset && d.State == c.State && d.Opacity == c.Opacity)),
            "Capture changed original logical arguments.");
        Results["capture-preserves-original-logical-arguments"] = true;

        cached.InvalidateVisual();
        hits = desktop.RenderCaches.Statistics.Hits;
        Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Invalidated brush reused pixels.");
        Queue();
        cached.Brush = new Source();
        hits = desktop.RenderCaches.Statistics.Hits;
        Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Replaced source reused pixels.");
        cached.Brush = source;
        Results["manual-and-source-replacement-invalidation"] = true;
        Queue();
        hits = desktop.RenderCaches.Statistics.Hits;
        source.Signal();
        Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Source revision mutation reused pixels.");
        Update();
        source.Signal();
        long builds = desktop.RenderCaches.Statistics.Builds;
        desktop.PrepareRenderCaches();
        Check(desktop.RenderCaches.Statistics.Builds == builds, "Source revision mutation published a stale request.");
        Results["source-revision-before-and-after-prepare"] = true;

        Queue();
        hits = desktop.RenderCaches.Statistics.Hits;
        var faded = Draw(true, 0.5f);
        Equal(faded, Draw(false, 0.5f), "fractional opacity fallback");
        Check(desktop.RenderCaches.Statistics.Hits == hits && source.Calls.Last().Opacity == 0.5f,
            "Opacity fallback changed arguments or reused pixels.");
        Queue();
        var clip = new Rectangle(40, 50, 17, 19);
        hits = desktop.RenderCaches.Statistics.Hits;
        Equal(Draw(true, 1, clip), Draw(false, 1, clip), "partial clip fallback");
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Partial clip used cached pixels.");
        Results["opacity-and-partial-clip-fallback"] = true;

        Queue();
        foreach (var owner in owners) { owner.IsRenderVolatile = true; }
        hits = desktop.RenderCaches.Statistics.Hits;
        Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits && desktop.RenderCaches.PendingCount == 0,
            "Explicit owner volatility was ignored.");
        foreach (var owner in owners) { owner.IsRenderVolatile = false; }
        Queue();
        hits = desktop.RenderCaches.Statistics.Hits;
        Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits + 4, "Cleared owner volatility did not recover.");
        Results["owner-volatility-and-recovery"] = true;

        int before = source.Calls.Count;
        hits = desktop.RenderCaches.Statistics.Hits;
        using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
        {
            cached.Draw(new(new(TimeSpan.Zero, transaction, 1), owners[0].VisualState, new Point(3, 4)), owners[0], new(10, 12, 8, 9));
        }
        Check(source.Calls.Count == before + 1 && desktop.RenderCaches.Statistics.Hits == hits,
            "Unknown direct caller incorrectly borrowed a built-in site.");
        Results["unknown-paint-site-direct-fallback"] = true;

        using Texture2D texture = new(GraphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        var textured = new MGTextureFillBrush(new MGTextureData(texture), Stretch.Fill);
        var asserted = new MGCachedFillBrush(textured) { SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic };
        using var graph = new MGRenderCacheSourceDependencies(asserted, static _ => { });
        graph.ReplaceSources(new object[] { asserted });
        Check(graph.SupportsRenderCaching && graph.ResourcesAreAlive, "Live asserted resource rejected.");
        texture.Dispose();
        Check(!asserted.SupportsRenderCaching && !graph.SupportsRenderCaching && !graph.ResourcesAreAlive,
            "Explicit-static wrapper hid disposed texture lifetime.");
        Results["explicit-static-retains-resource-lifetimes"] = true;
        var weakOwner = RegisterDetachedOwner(desktop, cached);
        source.Calls.Clear();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Check(!weakOwner.IsAlive, "Shared cached brush or live desktop/device retained a detached owner.");
        GC.KeepAlive(cached);
        Results["shared-brush-does-not-root-detached-owner"] = true;
        MGWindow churnWindow = owners[0].ParentWindow;
        var attachment = churnWindow.RenderRootAttachment;
        for (int cycle = 0; cycle < 16; cycle++)
        {
            var weak = RegisterChurnOwner(desktop, churnWindow, cached);
            source.Calls.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Check(!weak.IsAlive, "Content churn retained a detached owner.");
            Check(ReferenceEquals(attachment, churnWindow.RenderRootAttachment) && attachment.IsAttached,
                "Churn test detached its live root.");
            Check(MGRenderCacheOperation.GetRootListenerCount(desktop) <= 8, "Dead desktop operation listeners accumulated.");
        }
        Results["live-root-content-churn-bounds-weak-listeners"] = true;
        VerifyInactivePaintPath(desktop);
        Results["inactive-paint-path-retires-pending-and-ready"] = true;
        Completed = true;
    }
    private static void VerifyInactivePaintPath(MGDesktop desktop)
    {
        desktop.Windows.Clear();
        desktop.RenderCaches.Clear();
        MGWindow window = new(desktop, 40, 50, 180, 130) { WindowStyle = WindowStyle.None, Padding = new(0) };
        var source = new Source();
        var cached = new MGCachedFillBrush(source) { IsCacheEnabled = false };
        var child = new MGRectangle(window, 120, 80, Color.Transparent, 0, Color.Transparent) { BackgroundBrush = new(cached) };
        var inactive = new MGRectangle(window, 120, 80, Color.Transparent, 0, Color.Transparent);
        var contextual = new MGContextualContentPresenter(window, true, child, inactive);
        window.SetContent(contextual);
        desktop.Windows.Add(window);
        for (int i = 0; i < 4; i++) { desktop.Update(); desktop.Draw(); }
        cached.IsCacheEnabled = true;
        desktop.Update();
        desktop.Draw();
        Check(desktop.RenderCaches.PendingCount == 1, "Active contextual child did not queue.");
        var parent = child.Parent;
        var attachment = child.SelfOrParentWindow.RenderRootAttachment;
        ulong revision = child.VisualRevision;
        contextual.Value = false;
        Check(child.Parent == parent && child.SelfOrParentWindow.RenderRootAttachment == attachment && attachment.IsAttached,
            "Inactive-content check did not retain parent/root attachment.");
        Check(child.VisualRevision > revision && desktop.RenderCaches.PendingCount == 0,
            "Inactive paint path did not invalidate pending owner observation immediately.");
        int calls = source.Calls.Count;
        desktop.Update();
        desktop.PrepareRenderCaches();
        Check(source.Calls.Count == calls && desktop.RenderCaches.EntryCount == 0, "Inactive child captured queued pixels.");
        contextual.Value = true;
        for (int i = 0; i < 3; i++) { desktop.Update(); desktop.Draw(); }
        desktop.Update();
        desktop.PrepareRenderCaches();
        Check(desktop.RenderCaches.EntryCount == 1, "Reactivated child did not prepare.");
        revision = child.VisualRevision;
        contextual.Value = false;
        Check(child.VisualRevision > revision, "Ready owner observation survived paint-path removal.");
        calls = source.Calls.Count;
        desktop.Update();
        desktop.PrepareRenderCaches();
        Check(source.Calls.Count == calls && desktop.RenderCaches.EntryCount == 0, "Inactive ready target was retained as usable.");
        GC.KeepAlive(child);
    }

    private static MGElement CacheOnlyOwner(MGWindow window)
    {

        // Synthetic CPU owner isolates cache roots from preexisting UI constructor observers (mgui-9b9).
        // All target allocation, capture, readback and disposal still use the genuine native device.
        var owner = (MGRectangle)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MGRectangle));
        void Set(string name, object value) => typeof(MGElement).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(owner, value);
        Set("<ParentWindow>k__BackingField", window);
        Set("<ElementType>k__BackingField", MGElementType.Rectangle);
        Set("<Components>k__BackingField", new List<MGComponentBase>());
        Set("_IsLayoutValid", true);
        return owner;
    }

    private static void PaintCacheOnlyOwner(MGDesktop desktop, MGCachedFillBrush cached, MGElement owner)
    {
        using DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true);
        cached.DrawAtSite(new(new(TimeSpan.Zero, transaction, 1), default, Point.Zero), owner,
            new Rectangle(20, 20, 40, 30), MGFillBrushPaintSite.BackgroundUnderlay);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RegisterChurnOwner(MGDesktop desktop, MGWindow window, MGCachedFillBrush cached)
    {
        var owner = CacheOnlyOwner(window);
        PaintCacheOnlyOwner(desktop, cached, owner);
        return new WeakReference(owner);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RegisterDetachedOwner(MGDesktop desktop, MGCachedFillBrush cached)
    {
        var owner = CacheOnlyOwner(desktop.Windows[0]);
        PaintCacheOnlyOwner(desktop, cached, owner);
        desktop.Update();
        desktop.PrepareRenderCaches();
        PaintCacheOnlyOwner(desktop, cached, owner);
        return new WeakReference(owner);
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
    private readonly record struct Call(MGElement Owner, Rectangle Bounds, Point Offset, VisualState State, float Opacity, bool Capture);
    private sealed class Source : IFillBrush, IMGRenderCacheSource
    {
        internal readonly List<Call> Calls = new();
        public ulong VisualRevision { get; private set; } = 1;
        public bool SupportsRenderCaching => true;
        public bool IsRenderVolatile => false;
        public event EventHandler? VisualInvalidated;
        internal void Signal()
        {
            VisualRevision++;
            VisualInvalidated?.Invoke(this, EventArgs.Empty);
        }
        public IFillBrush Copy() => new Source();
        public void Draw(ElementDrawArgs args, MGElement owner, Rectangle bounds)
        {
            Calls.Add(new(owner, bounds, args.Offset, args.VisualState, args.Opacity, args.DT.IsCacheCapture));
            args.DT.FillRectangle(args.Offset.ToVector2(), bounds, (owner.Name == "owner0" ? Color.Teal : Color.Coral) * args.Opacity);
        }
    }
}
