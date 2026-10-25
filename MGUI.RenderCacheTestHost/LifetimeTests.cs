using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class LifetimeTests : Game
{
    private readonly Dictionary<string, bool> Checks = new();
    private readonly List<object> Comparisons = new();
    private readonly List<object> Accounting = new();
    private readonly Dictionary<RenderTarget2D, int> Targets = new();
    private string Evidence = "";
    private bool Completed;

    private LifetimeTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using LifetimeTests game = new();
        game.Run();
        Check(game.Completed, "Lifetime lane did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Checks));
        Console.WriteLine("ARTIFACTS:" + game.Evidence);
        return 0;
    }

    protected override void LoadContent()
    {
        Evidence = Path.Combine(Path.GetTempPath(), "mgui-lifetime-" + Environment.ProcessId);
        Directory.CreateDirectory(Evidence);
        TestNesting(2);
        TestNesting(3);
        TestNestedBrush();
        TestEligibility();
        TestManagedRemoval();
        TestReparenting();
        TestRawRootMembership();
        TestNestedWindowMembership();
        TestEviction();
        TestCaptureCancellation();
        TestSharedDeviceDesktops();
        TestWeakCacheOwnership();
        Check(Targets.Count > 0 && Targets.All(item => item.Key.IsDisposed && item.Value == 1),
            "A captured native target leaked or raised disposal more than once.");
        Checks.Add("all-tracked-native-targets-disposed-exactly-once", true);
        File.WriteAllText(Path.Combine(Evidence, "report.json"), JsonSerializer.Serialize(new
        {
            backend = "MonoGame DesktopGL", configuration = "Release", checks = Checks, comparisons = Comparisons,
            accounting = Accounting, trackedNativeTargets = Targets.Count,
            limitations = new[] { "Native Reset events exercised; raw context/driver removal is not simulated.",
                "Weak tests isolate cache manager/device/registration ownership with CPU identities. Whole UI collection is excluded because known mgui-9b9/mgui-pzh global observer retention is unrelated." },
            hostSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LifetimeTests).Assembly.Location))),
            coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MGRenderCache).Assembly.Location)))
        }, new JsonSerializerOptions { WriteIndented = true }));
        Completed = true;
        Exit();
    }

    private void TestNesting(int depth)
    {
        using Scene scene = new(this);
        Chain chain = scene.CreateChain(depth);
        scene.Settle();
        Check(scene.Manager.PendingCount == 0 && scene.Manager.EntryCount == 0, "Unobserved nesting allocated.");
        scene.Paint();
        Check(scene.Manager.PendingCount == depth, "Nested direct paint did not queue each boundary once.");
        long hits = scene.Manager.Statistics.Hits;
        long switches = scene.Manager.Statistics.TargetSwitches;
        int captures = chain.Leaf.Captures;
        scene.Tick();
        Check(scene.Manager.Statistics.Builds == depth && chain.Leaf.Captures == captures + depth
            && scene.Manager.PendingCount == 0 && scene.Manager.Statistics.Hits == hits
            && scene.Manager.Statistics.TargetSwitches == switches + 2 * depth,
            "Nested capture sampled a cache, queued recursively, or rebuilt recursively.");
        int paints = chain.Leaf.Paints;
        Color[] cached = scene.Paint();
        Check(chain.Leaf.Paints == paints && scene.Manager.Statistics.Hits == hits + 1, "Outer hit painted inner content.");
        Compare("nesting-" + depth, scene.Direct(chain), cached);
        scene.Warm(chain);
        ulong[] revisions = chain.Nodes.Select(node => node.VisualRevision).ToArray();
        chain.Leaf.Tint = Color.CornflowerBlue;
        chain.Leaf.InvalidateVisual();
        Check(chain.Nodes.Select((node, index) => node.VisualRevision > revisions[index]).All(value => value),
            "Leaf mutation did not invalidate every flattened ancestor.");
        hits = scene.Manager.Statistics.Hits;
        paints = chain.Leaf.Paints;
        Color[] changed = scene.Paint();
        Check(chain.Leaf.Paints == paints + 1 && scene.Manager.Statistics.Hits == hits && scene.Manager.PendingCount == depth,
            "Changed nested generation sampled stale pixels or failed to reobserve.");
        Check(!cached.SequenceEqual(changed), "Leaf color control did not change pixels.");
        long builds = scene.Manager.Statistics.Builds;
        scene.Tick();
        Check(scene.Manager.Statistics.Builds == builds + depth, "Changed nested generation failed to rebuild all boundaries.");
        Compare("nesting-" + depth + "-mutated", scene.Direct(chain), changed);
        scene.Warm(chain);
        long allocations = scene.Manager.Statistics.Allocations;
        builds = scene.Manager.Statistics.Builds;
        paints = chain.Leaf.Paints;
        for (int frame = 0; frame < 12; frame++)
        {
            scene.Tick();
            chain.Leaf.Discoveries = 0;
            scene.Paint();
            Check(chain.Leaf.Discoveries == 0, "Idle nested hit discovered the subtree.");
        }
        Check(scene.Manager.Statistics.Builds == builds && scene.Manager.Statistics.Allocations == allocations
            && chain.Leaf.Paints == paints, "Idle nested content rebuilt.");
        Checks.Add(depth + "-level-direct-capture-mutation-and-idle", true);
    }

    private void TestNestedBrush()
    {
        using Scene scene = new(this);
        Chain chain = scene.CreateChain(3);
        MGCachedFillBrush brush = new(new MGSolidFillBrush(Color.DarkSlateBlue));
        chain.Leaf.BackgroundBrush = new(brush);
        chain.Leaf.PaintInset = 10;
        scene.Settle();
        scene.Paint();
        Check(scene.Manager.PendingCount == 4, "Nested brush observation was not distinct from its three boundaries.");
        scene.Tick();
        Check(scene.Manager.Statistics.Builds == 4 && scene.Manager.PendingCount == 0,
            "Nested brush caused recursive capture requests.");
        long hits = scene.Manager.Statistics.Hits;
        Color[] cached = scene.Paint();
        Check(scene.Manager.Statistics.Hits == hits + 1, "Outer boundary depended on nested brush hits.");
        foreach (MGRenderCache node in chain.Nodes) { node.IsCacheEnabled = false; }
        brush.IsCacheEnabled = false;
        Compare("nested-brush", scene.Paint(), cached);
        Checks.Add("nested-brush-capture-never-depends-on-inner-target", true);
    }

    private void TestEligibility()
    {
        using Scene scene = new(this);
        Chain chain = scene.CreateChain(3);
        MGStackPanel panel = new(scene.Window, Orientation.Vertical);
        chain.Nodes[^1].SetContent(panel);
        panel.TryAddChild(chain.Leaf);
        scene.Warm(chain);
        MGRectangle volatileChild = new(scene.Window, 30, 20, Color.Black, 0, Color.Lime) { IsRenderVolatile = true };
        panel.TryAddChild(volatileChild);
        scene.Settle();
        long hits = scene.Manager.Statistics.Hits;
        long allocations = scene.Manager.Statistics.Allocations;
        for (int frame = 0; frame < 3; frame++)
        {
            scene.Tick(); scene.Paint();
            Check(chain.Nodes.All(node => node.LastBypassReason == MGRenderCacheBypassReason.Volatile),
                "Volatile descendant was hidden by a flattened ancestor.");
            Check(scene.Manager.PendingCount == 0 && scene.Manager.EntryCount == 0, "Volatile subtree retained cache work.");
        }
        Check(scene.Manager.Statistics.Hits == hits && scene.Manager.Statistics.Allocations == allocations,
            "Volatile subtree used or allocated a target.");
        panel.TryRemoveChild(volatileChild);
        scene.Warm(chain);
        UnknownElement unsupported = new(scene.Window);
        panel.TryAddChild(unsupported);
        scene.Settle();
        scene.Paint();
        Check(chain.Nodes.All(node => node.LastBypassReason == MGRenderCacheBypassReason.UnsupportedRenderer)
            && scene.Manager.PendingCount == 0, "Unsupported descendant did not bypass every enclosing boundary.");
        panel.TryRemoveChild(unsupported);
        scene.Warm(chain);
        Checks.Add("nested-volatility-and-unsupported-membership-recover", true);
    }

    private void TestManagedRemoval()
    {
        foreach (bool warm in new[] { false, true })
        {
            using Scene scene = new(this);
            Chain chain = scene.CreateChain(3);
            if (warm) { scene.Warm(chain); } else { scene.Settle(); scene.Paint(); }
            long builds = scene.Manager.Statistics.Builds;
            scene.Window.SetContent(new MGSpacer(scene.Window, 10, 10));
            Check(scene.Manager.PendingCount == 0 && chain.Leaf.RenderContentObserverSlots == 0
                && chain.Nodes.All(node => node.RenderContentBoundary.PublisherCount == 0), "Managed removal left nested registrations.");
            scene.Tick(); scene.Paint();
            Check(scene.Manager.EntryCount == 0 && scene.Manager.Statistics.Builds == builds, "Removed nested work was captured.");
            scene.Window.SetContent(chain.Nodes[0]);
            scene.Settle();
            Check(scene.Manager.Statistics.Builds == builds, "Reattachment captured without a new direct observation.");
            int paints = chain.Leaf.Paints;
            scene.Paint();
            Check(chain.Leaf.Paints == paints + 1 && scene.Manager.PendingCount == 3, "Reattachment reused an obsolete observation.");
            scene.Warm(chain);

            MGContentPresenter branch = new(scene.Window);
            scene.Window.SetContent((MGElement)null!);
            branch.SetContent(chain.Nodes[0]);
            MGContextualContentPresenter contextual = new(scene.Window, true, branch, new MGSpacer(scene.Window, 10, 10));
            scene.Window.SetContent(contextual);
            scene.Warm(chain);
            MGElement logicalParent = branch.Parent;
            contextual.Value = false;
            Check(ReferenceEquals(branch.Parent, logicalParent) && chain.Leaf.RenderContentObserverSlots == 0
                && chain.Nodes.All(node => node.RenderContentBoundary.PublisherCount == 0) && scene.Manager.PendingCount == 0,
                "Inactive logical-parent branch retained nested observers or queued work.");
            scene.Tick(); scene.Paint();
            Check(scene.Manager.EntryCount == 0, "Inactive subtree rebuilt or retained targets.");
            contextual.Value = true;
            scene.Warm(chain);

            Probe replacement = new(scene.Window, this);
            chain.Nodes[^1].SetContent(replacement);
            Check(chain.Leaf.RenderContentObserverSlots == 0 && scene.Manager.PendingCount == 0,
                "Replacing nested content retained old publishers.");
            scene.Warm(chain with { Leaf = replacement });
            Checks.Add(warm ? "warm-managed-detach-inactive-replace" : "pending-managed-detach-inactive-replace", true);
        }

        // Explicit public draws are independently observed sites, even outside the normal contributing tree.
        using Scene directScene = new(this);
        Chain directChain = directScene.CreateChain(2);
        directScene.Warm(directChain);
        Rectangle detachedBounds = directChain.Nodes[0].LayoutBounds;
        directScene.Window.SetContent((MGElement)null!);
        directScene.Settle();
        directScene.Paint(directChain.Nodes[0]);
        Check(directScene.Manager.PendingCount == 0, "Detached unresolved layout queued an observation.");
        directChain.Nodes[0].UpdateLayout(detachedBounds);
        directScene.Paint(directChain.Nodes[0]);
        Check(directScene.Manager.PendingCount == 2, "Explicit detached draw with a live ParentWindow did not observe its own paint.");
        directScene.Tick();
        int directPaints = directChain.Leaf.Paints;
        directScene.Paint(directChain.Nodes[0]);
        Check(directChain.Leaf.Paints == directPaints, "Explicit detached draw did not reuse its newly observed generation.");
        Checks.Add("explicit-detached-public-draw-is-a-new-observation", true);
    }

    private void TestReparenting()
    {
        using Scene scene = new(this);
        Chain chain = scene.CreateChain(2);
        scene.Window.SetContent((MGElement)null!);
        MGRenderCache oldOwner = new(scene.Window);
        MGRenderCache newOwner = new(scene.Window);
        oldOwner.SetContent(chain.Nodes[0]);
        MGStackPanel root = new(scene.Window, Orientation.Horizontal);
        oldOwner.PreferredWidth = 120; newOwner.PreferredWidth = 120;
        root.TryAddChild(oldOwner); root.TryAddChild(newOwner);
        scene.Window.SetContent(root);
        scene.Warm(new([oldOwner, .. chain.Nodes], chain.Leaf));
        ulong oldRevision = oldOwner.VisualRevision, newRevision = newOwner.VisualRevision;
        oldOwner.SetContent((MGElement)null!);
        newOwner.SetContent(chain.Nodes[0]);
        Check(oldOwner.VisualRevision > oldRevision && newOwner.VisualRevision > newRevision
            && scene.Manager.PendingCount == 0 && chain.Leaf.RenderContentObserverSlots == 0,
            "Reparenting failed to retire old/new owners and nested observations.");
        scene.Settle();
        Check(scene.Manager.EntryCount == 0, "Reparented targets survived without a new observation.");
        scene.Paint();
        Check(scene.Manager.PendingCount == 3, "Reparented new branch did not observe exactly three boundaries.");
        scene.Warm(new([newOwner, .. chain.Nodes], chain.Leaf));
        Color[] pixels = scene.Paint();
        Compare("reparented", scene.Direct(new([newOwner, .. chain.Nodes], chain.Leaf)), pixels);
        Checks.Add("reparent-invalidates-both-owners-and-reobserves", true);
    }

    private void TestRawRootMembership()
    {
        foreach (bool warm in new[] { false, true })
        {
            foreach (bool clear in new[] { false, true })
            {
                foreach (string lifecycle in new[] { "update", "prepare", "draw" })
                {
                    using Scene scene = new(this);
                    Chain chain = scene.CreateChain(3);
                    if (warm) { scene.Warm(chain); } else { scene.Settle(); scene.Paint(); }
                    MGRenderRootAttachment attachment = scene.Window.RenderRootAttachment;
                    long builds = scene.Manager.Statistics.Builds;
                    if (clear) { scene.Desktop.Windows.Clear(); } else { scene.Desktop.Windows.Remove(scene.Window); }
                    if (lifecycle == "update") { scene.Tick(false); }
                    if (lifecycle == "prepare") { scene.Desktop.PrepareRenderCaches(); }
                    if (lifecycle == "draw") { scene.PaintDesktop(); }
                    Check(!attachment.IsAttached && scene.Window.RenderRootAttachment == null && scene.Manager.PendingCount == 0
                        && chain.Leaf.RenderContentObserverSlots == 0
                        && chain.Nodes.All(node => node.RenderContentBoundary.PublisherCount == 0),
                        "Raw root " + lifecycle + " failed to retire pending/ready registrations.");
                    scene.Manager.Clear();
                    scene.Tick(); scene.PaintDesktop();
                    Check(scene.Manager.EntryCount == 0 && scene.Manager.Statistics.Builds == builds, "Raw removed root rebuilt.");
                    scene.Desktop.Windows.Add(scene.Window);
                    scene.Settle();
                    Check(!ReferenceEquals(attachment, scene.Window.RenderRootAttachment) && !attachment.IsAttached,
                        "Raw reinsertion revived the old attachment identity.");
                    Check(scene.Manager.Statistics.Builds == builds, "Raw reinsertion prepared an unobserved generation.");
                    int paints = chain.Leaf.Paints;
                    scene.PaintDesktop();
                    Check(chain.Leaf.Paints == paints + 1 && scene.Manager.PendingCount == 3,
                        "Raw reinsertion reused stale nested content.");
                    scene.Warm(chain);
                }
            }
        }
        Checks.Add("raw-remove-clear-reinsert-pending-warm-all-lifecycles", true);
    }

    private void TestNestedWindowMembership()
    {
        foreach (bool modal in new[] { false, true })
        {
            using Scene scene = new(this);
            MGWindow nested = new(scene.Window, 60, 70, 150, 120) { WindowStyle = WindowStyle.None, Padding = new(0) };
            Chain chain = scene.CreateChain(2, nested);
            if (modal) { scene.Window.ModalWindow = nested; } else { scene.Window.AddNestedWindow(nested); }
            scene.Warm(chain);
            MGRenderRootAttachment attachment = nested.RenderRootAttachment;
            if (modal) { scene.Window.ModalWindow = null; } else { scene.Window.RemoveNestedWindow(nested); }
            Check(!attachment.IsAttached && nested.RenderRootAttachment == null && scene.Manager.PendingCount == 0
                && chain.Leaf.RenderContentObserverSlots == 0, "Nested/modal detach did not immediately retire observations.");
            scene.Tick(); scene.Paint();
            Check(scene.Manager.EntryCount == 0, "Nested/modal removal retained native targets.");
            if (modal) { scene.Window.ModalWindow = nested; } else { scene.Window.AddNestedWindow(nested); }
            scene.Warm(chain);
        }
        Checks.Add("nested-and-modal-window-attachments-retire-independently", true);
    }

    private void TestEviction()
    {
        using Scene scene = new(this);
        Chain chain = scene.CreateChain(3);
        scene.Settle(); scene.Paint();
        scene.Manager.MaximumEntries = 1;
        Check(scene.Manager.PendingCount == 0, "Policy change retained queued generations.");
        scene.Paint(); scene.Tick();
        Check(scene.Manager.EntryCount == 1 && scene.Manager.PendingCount == 0, "One-entry nested budget was exceeded.");
        long evictions = scene.Manager.Statistics.Evictions;
        for (int frame = 0; frame < 6; frame++)
        {
            scene.Paint(); scene.Tick();
            Check(scene.Manager.EntryCount <= 1 && scene.Manager.RetainedBytes <= scene.Manager.MaximumRetainedBytes,
                "Nested budget pressure exceeded limits.");
        }
        Check(scene.Manager.Statistics.Evictions > evictions, "Nested preparation did not exercise eviction.");
        scene.Manager.MaximumEntries = 3;
        scene.Warm(chain);
        scene.Tick();
        RenderTarget2D preparedOuter = chain.Leaf.LastCaptureTarget!;
        scene.Manager.MaximumEntries = 1;
        Check(!preparedOuter.IsDisposed, "Budget reduction did not retain the recently composited outer target.");
        ulong outerRevision = chain.Nodes[0].VisualRevision;
        object competingOwner = new(), competingOperation = new();
        RenderTarget2D competing = BuildNative(scene.Manager, competingOwner, competingOperation);
        Check(preparedOuter.IsDisposed && scene.Manager.EntryCount == 1 && scene.Manager.Statistics.Evictions > evictions
            && chain.Nodes[0].VisualRevision == outerRevision,
            "Competing native request did not evict the observed outer target.");
        int paints = chain.Leaf.Paints;
        long hits = scene.Manager.Statistics.Hits;
        Color[] fallback = scene.Paint();
        Check(chain.Leaf.Paints == paints + 1 && scene.Manager.Statistics.Hits == hits && !competing.IsDisposed
            && scene.Manager.EntryCount == 1 && scene.Manager.PendingCount == 1,
            "Evicted outer target did not fall back directly before the reference toggle.");
        Compare("eviction-after-prepare", scene.Direct(chain), fallback);
        GC.KeepAlive(competingOwner); GC.KeepAlive(competingOperation);
        scene.Manager.MaximumRetainedBytes = 0;
        Check(scene.Manager.EntryCount == 0 && scene.Manager.PendingCount == 0, "Zero budget retained nested resources.");
        paints = chain.Leaf.Paints;
        scene.Tick(); scene.Paint();
        Check(chain.Leaf.Paints == paints + 1 && scene.Manager.EntryCount == 0, "Zero budget did not draw directly.");
        Checks.Add("nested-eviction-before-after-preparation-and-zero-budget", true);
    }

    private void TestCaptureCancellation()
    {
        foreach (string action in new[] { "invalidate", "detach", "clear", "throw" })
        {
            using Scene scene = new(this);
            Chain chain = scene.CreateChain(3);
            scene.Settle(); scene.Paint();
            chain.Leaf.DuringCapture = () =>
            {
                chain.Leaf.DuringCapture = null;
                if (action == "invalidate") { chain.Leaf.InvalidateVisual(); }
                if (action == "detach") { scene.Window.SetContent((MGElement)null!); }
                if (action == "clear") { scene.Manager.Clear(); }
                if (action == "throw") { throw new CaptureFailure(); }
            };
            bool threw = false;
            try { scene.Tick(); }
            catch (CaptureFailure) { threw = true; }
            Check(threw == (action == "throw") && scene.Manager.EntryCount == 0
                && (action == "throw" ? scene.Manager.PendingCount <= 2 : scene.Manager.PendingCount == 0)
                && scene.Manager.Statistics.Builds == 0 && GraphicsDevice.GetRenderTargets().Length == 0,
                "Nested capture cancellation published, leaked, or left a target bound: " + action);
            if (action == "detach") { scene.Window.SetContent(chain.Nodes[0]); }
            scene.Warm(chain);
        }
        Checks.Add("nested-capture-invalidation-detach-clear-and-exception", true);
    }

    private void TestSharedDeviceDesktops()
    {
        using Scene first = new(this);
        using Scene second = new(this);
        Chain a = first.CreateChain(2), b = second.CreateChain(2);
        b.Leaf.Tint = Color.ForestGreen;
        first.Warm(a); second.Warm(b);
        Check(!ReferenceEquals(first.Manager, second.Manager) && first.Manager.EntryCount == 2 && second.Manager.EntryCount == 2,
            "Shared device merged desktop managers.");
        first.Manager.MaximumEntries = 0;
        Check(first.Manager.EntryCount == 0 && second.Manager.EntryCount == 2, "One desktop budget evicted another desktop.");
        int paints = b.Leaf.Paints;
        long hits = second.Manager.Statistics.Hits;
        Color[] cached = second.Paint();
        Check(b.Leaf.Paints == paints && second.Manager.Statistics.Hits == hits + 1, "Other desktop lost its prepared target.");
        Compare("same-device-other-desktop-budget-isolation", second.Direct(b), cached);
        first.Manager.MaximumEntries = 64;
        first.Warm(a); second.Warm(b);
        a.Leaf.InvalidateVisual(); first.Paint();
        Check(first.Manager.PendingCount == 2 && second.Manager.EntryCount == 2, "Reset fixture lacks mixed pending/ready desktops.");
        GraphicsDevice.Reset();
        Check(first.Manager.EntryCount == 0 && second.Manager.EntryCount == 0 && first.Manager.PendingCount == 0
            && second.Manager.PendingCount == 0 && !first.Manager.IsDisposed && !second.Manager.IsDisposed,
            "Native reset did not invalidate every desktop on the device.");
        first.Tick(); second.Tick();
        paints = a.Leaf.Paints;
        first.Paint();
        Check(a.Leaf.Paints == paints + 1, "Reset sampled stale nested content.");
        first.Warm(a); second.Warm(b);
        second.Desktop.DisposeRenderCaches();
        Check(!first.Manager.IsDisposed && first.Manager.EntryCount == 2, "Desktop disposal crossed shared device owners.");
        first.Tick();
        paints = a.Leaf.Paints; first.Paint();
        Check(a.Leaf.Paints == paints, "Surviving desktop did not remain warm.");
        Checks.Add("same-device-desktop-budget-reset-and-dispose-isolation", true);
    }

    private void TestWeakCacheOwnership()
    {
        WeakReference abandoned = AbandonManager(GraphicsDevice);
        Collect();
        Check(!abandoned.IsAlive && !GraphicsDevice.IsDisposed, "Live device retained abandoned cache manager.");
        GraphicsDevice.Reset();
        using MGRenderCacheManager manager = new(GraphicsDevice);
        object operation = new();
        WeakReference owner = BuildCollectibleOwner(manager, operation);
        Collect();
        Check(!owner.IsAlive, "A native ready registration retained its CPU owner.");
        using (manager.BeginPreparation()) { }
        Check(manager.EntryCount == 0 && manager.Statistics.Allocations == manager.Statistics.Disposals,
            "Collected cache owner left native storage behind.");
        object liveOwner = new();
        WeakReference deadOperation = QueueCollectibleOperation(manager, liveOwner);
        Collect();
        Check(!deadOperation.IsAlive, "Pending registration retained its operation.");
        using (manager.BeginPreparation()) { }
        Check(manager.PendingCount == 0 && manager.EntryCount == 0, "Dead pending operation was not swept.");
        GC.KeepAlive(operation); GC.KeepAlive(liveOwner);
        Checks.Add("native-cache-manager-and-registration-weak-ownership", true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonManager(GraphicsDevice device) => new(new MGRenderCacheManager(device));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference BuildCollectibleOwner(MGRenderCacheManager manager, object operation)
    {
        object owner = new();
        BuildNative(manager, owner, operation);
        return new(owner);
    }

    private RenderTarget2D BuildNative(MGRenderCacheManager manager, object owner, object operation)
    {
        MGRenderCacheRegistration registration = manager.Register(owner, operation);
        Check(manager.Queue(registration, MGRenderCacheKey.Create(4, 4)), "Native fixture queue failed.");
        using (manager.BeginPreparation())
        {
            Check(manager.TryDequeue(out var request), "Weak owner request missing.");
            Check(manager.TryAllocate(request, out var candidate), "Weak owner allocation failed.");
            using (candidate)
            {
                Track(candidate.Target);
                GraphicsDevice.SetRenderTarget(candidate.Target);
                GraphicsDevice.Clear(Color.Gold);
                GraphicsDevice.SetRenderTarget(null);
                RenderTarget2D target = candidate.Target;
                Check(candidate.Publish(), "Native fixture target failed to publish.");
                return target;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference QueueCollectibleOperation(MGRenderCacheManager manager, object owner)
    {
        object operation = new();
        Check(manager.Queue(manager.Register(owner, operation), MGRenderCacheKey.Create(4, 4)), "Weak operation queue failed.");
        return new(operation);
    }

    private static void Collect()
    {
        for (int attempt = 0; attempt < 3; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    }

    private void Track(RenderTarget2D target)
    {
        if (Targets.TryAdd(target, 0))
        {
            target.Disposing += (_, _) => Targets[target]++;
        }
        Check(!target.IsDisposed, "Capture used a disposed target.");
    }

    private void Compare(string name, Color[] direct, Color[] cached)
    {
        int different = direct.Where((pixel, index) => pixel != cached[index]).Count();
        Comparisons.Add(new { name, differentPixels = different, allowedPerChannelError = 0 });
        using Texture2D image = new(GraphicsDevice, 640, 480);
        foreach (var item in new[] { ("direct", direct), ("cached", cached) })
        {
            image.SetData(item.Item2);
            using FileStream output = File.Create(Path.Combine(Evidence, name + "-" + item.Item1 + ".png"));
            image.SaveAsPng(output, 640, 480);
        }
        Check(different == 0, name + " exact RGBA mismatch; artifacts " + Evidence);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private sealed record Chain(MGRenderCache[] Nodes, Probe Leaf);
    private sealed class CaptureFailure : Exception { }
    private sealed class UnknownElement(MGWindow window) : MGContentPresenter(window) { }

    private sealed class Probe(MGWindow window, LifetimeTests tests) : MGElement(window, MGElementType.Rectangle)
    {
        internal Color Tint = Color.OrangeRed;
        internal int Paints, Captures, Discoveries, PaintInset;
        internal RenderTarget2D? LastCaptureTarget;
        internal Action? DuringCapture;
        protected override bool SupportsRenderCaching => true;
        public override IEnumerable<MGElement> GetChildren() { Discoveries++; return Array.Empty<MGElement>(); }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            if (args.DT.IsCacheCapture)
            {
                Captures++;
                RenderTargetBinding[] bindings = args.DT.GD.GetRenderTargets();
                Check(bindings.Length == 1 && bindings[0].RenderTarget is RenderTarget2D, "Capture has no unique native target.");
                LastCaptureTarget = (RenderTarget2D)bindings[0].RenderTarget;
                tests.Track(LastCaptureTarget);
                DuringCapture?.Invoke();
            }
            else { Paints++; }
            bounds.Inflate(-PaintInset, -PaintInset);
            new MGSolidFillBrush(Tint).Draw(args, this, bounds);
            new MGSolidFillBrush(Color.DarkBlue).Draw(args, this, new(bounds.X + 7, bounds.Y + 9, 19, 17));
        }
    }

    private sealed class Scene : IDisposable
    {
        private readonly LifetimeTests Tests;
        private readonly BenchmarkFixture Fixture;
        private int Frame;
        internal MGDesktop Desktop => Fixture.Desktop;
        internal MGRenderCacheManager Manager => Desktop.RenderCaches;
        internal MGWindow Window { get; }
        internal Scene(LifetimeTests tests)
        {
            Tests = tests;
            Fixture = new(tests, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
            Desktop.Windows.Clear();
            Window = new(Desktop, 40, 50, 260, 200) { WindowStyle = WindowStyle.None, Padding = new(0), BackgroundBrush = new((IFillBrush)null!) };
            Desktop.Windows.Add(Window);
        }
        internal Chain CreateChain(int depth, MGWindow? window = null)
        {
            window ??= Window;
            Probe leaf = new(window, Tests);
            MGRenderCache[] nodes = Enumerable.Range(0, depth).Select(_ => new MGRenderCache(window) { Padding = new(2) }).ToArray();
            for (int index = 0; index < depth; index++) { nodes[index].SetContent(index == depth - 1 ? leaf : nodes[index + 1]); }
            window.SetContent(nodes[0]);
            return new(nodes, leaf);
        }
        internal void Tick(bool prepare = true)
        {
            Fixture.Update(++Frame);
            if (prepare) { Desktop.PrepareRenderCaches(); }
            Check(Manager.EntryCount <= Manager.MaximumEntries && Manager.RetainedBytes <= Manager.MaximumRetainedBytes,
                "Native storage exceeded configured bounds.");
        }
        internal void Settle() { for (int frame = 0; frame < 3; frame++) { Tick(); } }
        internal void Warm(Chain chain)
        {
            foreach (MGRenderCache node in chain.Nodes) { node.IsCacheEnabled = true; }
            for (int frame = 0; frame < 5; frame++) { Tick(); Paint(); }
            int paints = chain.Leaf.Paints;
            long hits = Manager.Statistics.Hits;
            Tick(); Paint();
            Check(chain.Leaf.Paints == paints && Manager.Statistics.Hits > hits,
                "Nested content did not warm: " + string.Join(",", chain.Nodes.Select(node => node.LastBypassReason)));
        }
        internal Color[] Direct(Chain chain)
        {
            foreach (MGRenderCache node in chain.Nodes) { node.IsCacheEnabled = false; }
            return Paint();
        }
        internal Color[] Paint(MGElement? painter = null) => Draw(painter, false);
        internal Color[] PaintDesktop() => Draw(null, true);
        private Color[] Draw(MGElement? painter, bool desktop)
        {
            GraphicsDevice device = Tests.GraphicsDevice;
            device.SetRenderTarget(null);
            device.ScissorRectangle = device.Viewport.Bounds;
            device.Clear(new Color(19, 37, 53));
            long switches = device.Metrics.TargetCount, allocations = Manager.Statistics.Allocations;
            using (DrawTransaction transaction = new(Desktop.Renderer, DrawSettings.Default, true))
            {
                transaction.FillRectangle(Vector2.Zero, new Rectangle(470, 380, 31, 27), Color.Lime);
                if (desktop) { Desktop.Draw(transaction); }
                else
                {
                    MGElement element = painter ?? Window;
                    element.Draw(new(new(TimeSpan.Zero, transaction, 1), element.VisualState, Point.Zero));
                }
            }
            Check(device.Metrics.TargetCount == switches && Manager.Statistics.Allocations == allocations,
                "Final draw allocated a target or switched targets.");
            Color[] pixels = new Color[640 * 480];
            device.GetBackBufferData(pixels);
            Check(pixels[390 * 640 + 480] == Color.Lime, "Lifetime rendering destroyed scene pixels.");
            return pixels;
        }
        public void Dispose()
        {
            Desktop.DisposeRenderCaches();
            Check(Manager.EntryCount == 0 && Manager.RetainedBytes == 0 && Manager.PendingCount == 0
                && Manager.Statistics.Allocations == Manager.Statistics.Disposals,
                "Scene teardown leaked native cache resources.");
            Tests.Accounting.Add(Manager.Statistics);
            Fixture.Dispose();
        }
    }
}
