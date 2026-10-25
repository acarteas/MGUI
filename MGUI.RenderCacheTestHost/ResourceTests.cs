using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MGUI.RenderCacheTestHost;

// Separate resource mode keeps the accepted benchmark fixture and its retained baseline artifacts unchanged.
internal sealed class ResourceTests : Game
{
    private readonly GraphicsDeviceManager Graphics;
    private readonly Dictionary<string, bool> Results = new();
    private readonly List<object> Identities = new();
    private readonly List<RenderTarget2D> Created = new();
    private int DisposeEvents;
    private bool Completed;
    internal ResourceTests()
    {
        Graphics = new(this) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 240, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Window.Title = "MGUI cache resource contract";
    }
    internal static int RunAllNative()
    {
        using ResourceTests game = new();
        game.Run();
        Check(game.Completed, "Resource tests did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:" + JsonSerializer.Serialize(new
        {
            backend = "MonoGame DesktopGL", profile = game.GraphicsDevice.GraphicsProfile.ToString(),
            configuration =
#if DEBUG
                "Debug",
#else
                "Release",
#endif
            targetsCreated = game.Created.Count, targetDisposalEvents = game.DisposeEvents,
            framework = typeof(Game).Assembly.FullName,
            display = Environment.GetEnvironmentVariable("DISPLAY"),
            renderer = "See owned display glxinfo in resource runner output; no hardware FPS inference."
        }));
        return 0;
    }
    protected override void LoadContent()
    {
        SourceTests.Run(GraphicsDevice);
        Pass("native-source-lifetimes-manual-invalidation-weak-owners");
        TestNativeAllocationAndOwnership();
        TestDeferredSampledAndBoundRetirement(false);
        TestDeferredSampledAndBoundRetirement(true);
        TestNativeRetirementCallback(false);
        TestNativeRetirementCallback(true);
        TestFirstBindFailureAndReset();
        TestDesktopOwnership();
        TestWeakDeviceRoots();
        TestSeparateDevices();
        Check(Created.All(x => x.IsDisposed), "A cache-owned native target survived teardown.");
        Check(DisposeEvents == Created.Count, "A native target was not disposed exactly once.");
        Pass("all-native-targets-released-once");
        Completed = true;
    }
    protected override void Update(GameTime gameTime) { Exit(); }
    protected override void Draw(GameTime gameTime) { }

    private void TestNativeAllocationAndOwnership()
    {
        using MGRenderCacheManager manager = new(GraphicsDevice);
        Check(manager.EntryCount == 0 && manager.RetainedBytes == 0 && manager.Statistics.Allocations == 0, "Unused manager allocated targets.");
        var a = Registration(manager);
        Check(manager.Queue(a, Key(8, 4)), "Queue failed.");
        Check(manager.EntryCount == 0 && manager.Statistics.Allocations == 0, "Queue allocated targets.");
        using Texture2D suppliedTexture = new(GraphicsDevice, 1, 1);
        suppliedTexture.SetData(new[] { Color.White });
        using BasicEffect suppliedEffect = new(GraphicsDevice);
        RenderTarget2D first = Build(manager, a, Key(8, 4));
        Check(first.LevelCount == 1 && first.Format == SurfaceFormat.Color && first.DepthStencilFormat == DepthFormat.None
            && first.MultiSampleCount == 0 && first.RenderTargetUsage == RenderTargetUsage.DiscardContents, "Wrong target settings.");
        Color[] pixels = new Color[32];
        first.GetData(pixels);
        Check(pixels.All(x => x == Color.CornflowerBlue), "Real first binding/clear/readback failed.");
        Check(manager.RetainedBytes == 128 && manager.EntryCount == 1, "Native byte accounting failed.");
        manager.Clear(); manager.Clear(); manager.Dispose(); manager.Dispose();
        Check(first.IsDisposed && !suppliedTexture.IsDisposed && !suppliedEffect.IsDisposed && !GraphicsDevice.IsDisposed,
            "Manager disposed caller resources or failed to release owned target.");
        Pass("native-settings-first-bind-readback");
        Pass("caller-resource-ownership");
        Pass("unused-and-queued-cpu-only");
    }

    private void TestDeferredSampledAndBoundRetirement(bool dispose)
    {
        using MGRenderCacheManager manager = new(GraphicsDevice);
        var a = Registration(manager); var b = Registration(manager);
        RenderTarget2D sampled = Build(manager, a, Key(4, 4));
        Check(manager.Queue(b, Key(4, 4)), "Queue failed.");
        using (manager.BeginPreparation())
        {
            Check(manager.TryDequeue(out var request), "Missing native request.");
            Check(manager.TryAllocate(request, out var candidate), "Native candidate allocation failed.");
            using (candidate)
            {
                Track(candidate.Target);
                GraphicsDevice.SetRenderTarget(candidate.Target);
                GraphicsDevice.Clear(Color.Transparent);
                using SpriteBatch sprites = new(GraphicsDevice);
                sprites.Begin();
                sprites.Draw(sampled, new Rectangle(0, 0, 4, 4), Color.White);
                if (dispose) { manager.Dispose(); } else { manager.Clear(); }
                Check(!sampled.IsDisposed && !candidate.Target.IsDisposed && manager.EntryCount == 2 && manager.RetainedBytes == 128,
                    "Clear/Dispose released a queued sampled/bound native target.");
                sprites.End();
                GraphicsDevice.SetRenderTarget(null);
                Check(!candidate.Publish(), "Cancelled candidate published.");
            }
        }
        Check(manager.EntryCount == 0 && manager.RetainedBytes == 0 && sampled.IsDisposed, "Retirement did not drain.");
        Pass(dispose ? "reentrant-dispose-sampled-bound-safe" : "reentrant-clear-sampled-bound-safe");
    }

    private void TestNativeRetirementCallback(bool dispose)
    {
        using MGRenderCacheManager manager = new(GraphicsDevice);
        var a = Registration(manager); var b = Registration(manager); var c = Registration(manager);
        RenderTarget2D earliest = Build(manager, a, Key());
        RenderTarget2D later = Build(manager, b, Key());
        RenderTarget2D latest = Build(manager, c, Key());
        earliest.Disposing += (_, _) =>
        {
            if (dispose) { manager.Dispose(); } else { manager.Clear(); }
        };
        a.Invalidate();
        using var scope = manager.BeginPreparation();
        Check((scope == null) == dispose && earliest.IsDisposed && later.IsDisposed && latest.IsDisposed
            && manager.EntryCount == 0 && manager.RetainedBytes == 0,
            "A native disposal callback left later retired targets allocated.");
        Pass(dispose ? "native-dispose-callback-drains-all" : "native-clear-callback-drains-all");
    }

    private void TestFirstBindFailureAndReset()
    {
        using MGRenderCacheManager manager = new(GraphicsDevice);
        var a = Registration(manager);
        Check(manager.Queue(a, Key(4, 4)), "Queue failed.");
        RenderTarget2D rejected;
        Exception failure = new InvalidOperationException("synthetic failure after genuine native first bind and clear");
        using (manager.BeginPreparation())
        {
            Check(manager.TryDequeue(out var request), "Missing native request.");
            Check(manager.TryAllocate(request, out var candidate), "Allocation failed.");
            rejected = candidate.Target;
            Track(rejected);
            GraphicsDevice.SetRenderTarget(rejected);
            GraphicsDevice.Clear(Color.Transparent);
            GraphicsDevice.SetRenderTarget(null);
            candidate.RejectNativeFailure(failure);
        }
        Check(rejected.IsDisposed && manager.EntryCount == 0 && manager.RetainedBytes == 0 && ReferenceEquals(manager.LastNativeFailure, failure),
            "Native bind-failure release/diagnostic failed.");
        Check(!manager.Queue(a, Key(4, 4)), "Failed native size was retried.");
        manager.MaximumTargetDimension++;
        RenderTarget2D ready = Build(manager, a, Key(4, 4));
        var b = Registration(manager);
        Check(manager.Queue(b, Key()), "Queue before reset failed.");
        GraphicsDevice.Reset();
        Check(ready.IsDisposed && manager.EntryCount == 0 && manager.PendingCount == 0 && !manager.IsDisposed,
            "Real device reset did not invalidate cache state.");
        Build(manager, a, Key(4, 4));
        manager.Dispose();
        Pass("native-first-bind-failure-release-suppression");
        Pass("native-device-reset-forwarding");
    }

    private void TestDesktopOwnership()
    {
        Options options = new("hud", 320, 240, 1, 1729, 0, 1, "", "");
        using BenchmarkFixture fixture = new(this, options);
        var service = fixture.Desktop.RenderCaches;
        Check(service.EntryCount == 0 && service.RetainedBytes == 0, "Constructing desktop allocated cache targets.");
        var registration = Registration(service);
        RenderTarget2D target = Build(service, registration, Key());
        fixture.Desktop.DisposeRenderCaches();
        Check(ReferenceEquals(service, fixture.Desktop.RenderCaches) && service.IsDisposed && target.IsDisposed,
            "Desktop cleanup did not permanently disable its original service.");
        Check(service.BeginPreparation() == null && !service.Queue(registration, Key()), "Disposed desktop service resurrected.");
        Pass("desktop-owned-same-service-permanent-disposal");
    }

    private void TestWeakDeviceRoots()
    {
        WeakReference abandoned = AbandonManager(GraphicsDevice);
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check(!abandoned.IsAlive && !GraphicsDevice.IsDisposed, "A live device event rooted an abandoned manager.");
        GraphicsDevice.Reset();
        using MGRenderCacheManager manager = new(GraphicsDevice);
        var owner = AddCollectibleOwner(manager);
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check(!owner.IsAlive, "A ready native entry rooted its owner.");
        using (manager.BeginPreparation()) { Check(manager.EntryCount == 0, "Collected owner target was not swept."); }
        Pass("live-device-weak-manager-and-owner");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonManager(GraphicsDevice device)
    {
        MGRenderCacheManager manager = new(device);
        return new(manager);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference AddCollectibleOwner(MGRenderCacheManager manager)
    {
        object owner = new(), operation = new();
        Identities.Add(operation);
        Build(manager, manager.Register(owner, operation), Key());
        return new(owner);
    }
    private void TestSeparateDevices()
    {
        PresentationParameters parameters = new()
        {
            BackBufferWidth = 32, BackBufferHeight = 32, DeviceWindowHandle = Window.Handle,
            IsFullScreen = false, PresentationInterval = PresentInterval.Immediate
        };
        using GraphicsDevice otherDevice = new(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, parameters);
        using MGRenderCacheManager other = new(otherDevice);
        using MGRenderCacheManager original = new(GraphicsDevice);
        var a = Registration(original); var b = Registration(other);
        RenderTarget2D originalTarget = Build(original, a, Key());
        RenderTarget2D otherTarget = Build(other, b, Key(), otherDevice);
        Check(ReferenceEquals(originalTarget.GraphicsDevice, GraphicsDevice) && ReferenceEquals(otherTarget.GraphicsDevice, otherDevice),
            "Targets crossed devices.");
        Check(!other.Queue(a, Key()) && !original.Queue(b, Key()), "Cross-device registration was accepted.");
        otherDevice.Dispose();
        Check(other.IsDisposed && otherTarget.IsDisposed && !original.IsDisposed && !originalTarget.IsDisposed,
            "Device disposal did not isolate/disable the correct manager.");
        original.Dispose();
        Pass("native-multiple-devices-isolated-and-device-disposal");
    }
    private MGRenderCacheRegistration Registration(MGRenderCacheManager manager)
    {
        object owner = new(), operation = new();
        Identities.Add(owner); Identities.Add(operation);
        return manager.Register(owner, operation);
    }
    private RenderTarget2D Build(MGRenderCacheManager manager, MGRenderCacheRegistration registration, MGRenderCacheKey key, GraphicsDevice? device = null)
    {
        device ??= GraphicsDevice;
        Check(manager.Queue(registration, key), "Native queue failed.");
        using (manager.BeginPreparation())
        {
            Check(manager.TryDequeue(out var request), "Missing native request.");
            Check(manager.TryAllocate(request, out var candidate), "Native allocation failed.");
            using (candidate)
            {
                RenderTarget2D target = candidate.Target;
                Track(target);
                device.SetRenderTarget(target);
                device.Clear(Color.CornflowerBlue);
                device.SetRenderTarget(null);
                Check(candidate.Publish(), "Native candidate did not publish.");
                return target;
            }
        }
    }
    private void Track(RenderTarget2D target)
    {
        Created.Add(target);
        target.Disposing += (_, _) => DisposeEvents++;
    }
    private static MGRenderCacheKey Key(int width = 1, int height = 1) => MGRenderCacheKey.Create(width, height);
    private void Pass(string name) { Results.Add(name, true); }
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
