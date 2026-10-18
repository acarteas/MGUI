using MGUI.Core.UI;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public class RenderCacheManagerTests
{
    [Fact]
    public void DefaultsAndRegistrationAreCpuOnly()
    {
        using Harness h = new();
        Assert.Equal(33_554_432, h.Manager.MaximumRetainedBytes);
        Assert.Equal(64, h.Manager.MaximumEntries);
        Assert.Equal(4096, h.Manager.MaximumTargetDimension);
        var registration = h.Register();
        Assert.False(h.Manager.TryGetResource(registration, Key(), out _));
        Assert.True(h.Manager.Queue(registration, Key()));
        Assert.Empty(h.Allocator.Targets);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(0, h.Manager.RetainedBytes);
        Assert.Equal(1, h.Manager.PendingCount);
    }

    [Fact]
    public void EntryLimitEvictsLeastRecentlyCompositedWithCreationTieBreak()
    {
        using Harness h = new();
        h.Manager.MaximumEntries = 2;
        var a = h.Register(); var b = h.Register(); var c = h.Register(); var d = h.Register();
        h.Build(a); h.Build(b);
        Assert.True(h.Manager.TryGetResource(a, Key(), out _));
        h.Build(c);
        Assert.True(h.Allocator.Targets[1].IsDisposed);
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        h.Build(d);
        Assert.True(h.Allocator.Targets[2].IsDisposed);
        Assert.Equal(2, h.Manager.EntryCount);
        Assert.Equal(2, h.Manager.Statistics.Evictions);

        using Harness ties = new();
        ties.Manager.MaximumEntries = 2;
        ties.Build(ties.Register()); ties.Build(ties.Register()); ties.Build(ties.Register());
        Assert.True(ties.Allocator.Targets[0].IsDisposed);
        Assert.False(ties.Allocator.Targets[1].IsDisposed);
    }

    [Fact]
    public void ByteLimitAndReplacementNeverOvercommitIncludingInFlight()
    {
        using Harness h = new();
        h.Manager.MaximumRetainedBytes = 32;
        var a = h.Register(); var b = h.Register();
        h.Build(a, Key(2, 2)); h.Build(b, Key(2, 2));
        a.Invalidate();
        Assert.Equal(32, h.Manager.RetainedBytes);
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        Assert.True(h.Manager.Queue(a, Key(2, 3)));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.True(h.Manager.TryAllocate(request, out var candidate));
            Assert.Equal(24, h.Manager.RetainedBytes);
            Assert.Equal(1, h.Manager.EntryCount);
            Assert.True(h.Allocator.Targets[0].IsDisposed);
            Assert.True(h.Allocator.Targets[1].IsDisposed);
            Assert.True(candidate.Publish());
        }
        Assert.All(h.Allocator.Accounting, x => Assert.InRange(x.Bytes, 0, 32));
    }

    [Fact]
    public void LimitsRejectBeforeConstructionIncludingOverflow()
    {
        using Harness h = new();
        var a = h.Register();
        Assert.False(h.Manager.Queue(a, Key(0, 2)));
        Assert.Equal(MGRenderCacheBypassReason.InvalidSize, h.Manager.Statistics.LastBypassReason);
        Assert.False(h.Manager.Queue(a, Key(-1, 2)));
        Assert.False(h.Manager.Queue(a, Key(4097, 2)));
        Assert.Equal(MGRenderCacheBypassReason.DimensionLimit, h.Manager.Statistics.LastBypassReason);
        h.Manager.MaximumTargetDimension = int.MaxValue;
        h.Manager.MaximumRetainedBytes = long.MaxValue;
        Assert.False(h.Manager.Queue(a, Key(int.MaxValue, int.MaxValue)));
        Assert.Equal(MGRenderCacheBypassReason.Budget, h.Manager.Statistics.LastBypassReason);
        h.Manager.MaximumRetainedBytes = 15;
        Assert.False(h.Manager.Queue(a, Key(2, 2)));
        Assert.Empty(h.Allocator.Targets);
    }

    [Fact]
    public void PolicyReductionsAndZeroReleaseAtSafePointAndInvalidateRequests()
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register(); var c = h.Register();
        h.Build(a, Key(4, 4)); h.Build(b, Key(2, 2));
        Assert.True(h.Manager.Queue(c, Key()));
        h.Manager.MaximumTargetDimension = 2;
        Assert.True(h.Allocator.Targets[0].IsDisposed);
        Assert.False(h.Allocator.Targets[1].IsDisposed);
        Assert.Equal(0, h.Manager.PendingCount);
        Assert.True(h.Manager.TryGetResource(b, Key(2, 2), out _));
        h.Manager.MaximumEntries = 0;
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(0, h.Manager.RetainedBytes);
        Assert.False(h.Manager.Queue(a, Key()));
        h.Manager.MaximumEntries = 1;
        h.Build(a);
        h.Manager.MaximumRetainedBytes = 0;
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.False(h.Manager.Queue(a, Key()));
    }

    [Fact]
    public void ByteAndEntryReductionsUseLruAndInvalidSettersDoNotMutate()
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register(); var c = h.Register();
        h.Build(a); h.Build(b); h.Build(c);
        Assert.True(h.Manager.TryGetResource(a, Key(), out _));
        h.Manager.MaximumRetainedBytes = 8;
        Assert.True(h.Allocator.Targets[1].IsDisposed);
        h.Manager.MaximumEntries = 1;
        Assert.True(h.Allocator.Targets[2].IsDisposed);
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Manager.MaximumRetainedBytes = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Manager.MaximumEntries = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Manager.MaximumTargetDimension = 0);
        Assert.Equal(8, h.Manager.MaximumRetainedBytes);
        Assert.Equal(1, h.Manager.MaximumEntries);
    }

    [Fact]
    public void QueueIsBoundedDeduplicatedNewestAndKeepsInsertionOrder()
    {
        using Harness h = new();
        h.Manager.MaximumEntries = 2;
        var a = h.Register(); var b = h.Register(); var c = h.Register();
        Assert.True(h.Manager.Queue(a, Key()));
        Assert.True(h.Manager.Queue(b, Key()));
        var newest = Key(2, 1) with { SourceRevision = 2 };
        Assert.True(h.Manager.Queue(a, newest));
        Assert.False(h.Manager.Queue(c, Key()));
        Assert.Equal(MGRenderCacheBypassReason.QueueFull, h.Manager.Statistics.LastBypassReason);
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var first));
            Assert.Same(a, first.Registration);
            Assert.Equal(newest, first.Key);
            Assert.True(h.Manager.TryDequeue(out var second));
            Assert.Same(b, second.Registration);
            Assert.False(h.Manager.TryDequeue(out _));
        }
    }

    [Fact]
    public void RegistrationSeparatesOwnersAndPaintOperations()
    {
        using Harness h = new();
        object owner = new(), otherOwner = new(), operation = new(), otherOperation = new();
        var a = h.Manager.Register(owner, operation);
        Assert.Same(a, h.Manager.Register(owner, operation));
        var b = h.Manager.Register(otherOwner, operation);
        var c = h.Manager.Register(owner, otherOperation);
        h.Build(a); h.Build(b); h.Build(c);
        Assert.Equal(3, h.Manager.EntryCount);
        Assert.True(h.Manager.TryGetResource(a, Key(), out var ar));
        Assert.True(h.Manager.TryGetResource(b, Key(), out var br));
        Assert.True(h.Manager.TryGetResource(c, Key(), out var cr));
        Assert.NotSame(ar, br); Assert.NotSame(ar, cr);
        GC.KeepAlive(owner); GC.KeepAlive(otherOwner); GC.KeepAlive(operation); GC.KeepAlive(otherOperation);
    }

    [Fact]
    public void DesktopsAndAllocatorDevicesCannotShareRegistrationsOrEntries()
    {
        using Harness a = new(); using Harness b = new();
        var registration = a.Register();
        a.Build(registration);
        Assert.False(b.Manager.Queue(registration, Key()));
        Assert.False(b.Manager.TryGetResource(registration, Key(), out _));
        Assert.Equal(0, b.Manager.EntryCount);
        Assert.Empty(b.Allocator.Targets);
    }

    [Fact]
    public void InvalidationImmediatelyPreventsHitsButDefersTargetDestruction()
    {
        using Harness h = new();
        var a = h.Register();
        h.Build(a);
        Assert.True(h.Manager.TryGetResource(a, Key(), out _));
        a.Invalidate();
        Assert.False(h.Manager.TryGetResource(a, Key(), out _));
        Assert.Equal(1, h.Manager.EntryCount);
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        using (h.Manager.BeginPreparation()) { Assert.Equal(0, h.Manager.EntryCount); }
        Assert.Equal(1, h.Allocator.Targets[0].DisposeCalls);
    }

    [Fact]
    public void AllContextAndRevisionChangesMissWithoutReturningOldPixels()
    {
        using Harness h = new();
        var a = h.Register();
        h.Build(a);
        var variants = new[]
        {
            Key() with { VisualRevision = 2 }, Key() with { LayoutRevision = 2 }, Key() with { StructureRevision = 2 },
            Key() with { SourceRevision = 2 }, Key() with { Observation = 2 }, Key() with { RenderContext = 2 },
            Key() with { LayoutResolution = 2 }, Key() with { Offset = new(1, 0) }, Key() with { Bounds = new(1, 0, 1, 1) },
            Key() with { VisualState = 1 }, Key() with { Opacity = .5f },
            Key() with { Transform = Microsoft.Xna.Framework.Matrix.CreateTranslation(1, 0, 0) }
        };
        foreach (var variant in variants) { Assert.False(h.Manager.TryGetResource(a, variant, out _)); }
    }

    [Fact]
    public void DequeuedRequestRejectsRegistrationAndServiceGenerationChanges()
    {
        using Harness h = new();
        var a = h.Register();
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var stale));
            a.Invalidate();
            Assert.False(h.Manager.TryAllocate(stale, out _));
        }
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var stale));
            h.Manager.Clear();
            Assert.False(h.Manager.TryAllocate(stale, out _));
        }
        Assert.Empty(h.Allocator.Targets);
    }

    [Fact]
    public void EpochRolloverRejectsPendingAndReadyWithoutCounterAliasing()
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register();
        var old = Key();
        h.Build(a, old);
        Assert.True(h.Manager.Queue(b, old));
        MGRenderRevisionClock.RestartEpoch();
        Assert.False(h.Manager.TryGetResource(a, old, out _));
        Assert.Equal(0, h.Manager.PendingCount);
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        using (h.Manager.BeginPreparation()) { Assert.False(h.Manager.TryDequeue(out _)); }
        Assert.True(h.Allocator.Targets[0].IsDisposed);
        h.Build(a);
        Assert.True(h.Manager.TryGetResource(a, Key(), out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentrantClearOrDisposeDefersAllSampledAndBoundTargets(bool dispose)
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register();
        h.Build(a);
        Assert.True(h.Manager.Queue(b, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.True(h.Manager.TryAllocate(request, out var candidate));
            if (dispose) { h.Manager.Dispose(); } else { h.Manager.Clear(); }
            Assert.Equal(2, h.Manager.EntryCount);
            Assert.Equal(8, h.Manager.RetainedBytes);
            Assert.All(h.Allocator.Targets, x => Assert.False(x.IsDisposed));
            Assert.False(candidate.Publish());
            Assert.Equal(0, h.Manager.EntryCount);
        }
        Assert.All(h.Allocator.Targets, x => Assert.Equal(1, x.DisposeCalls));
        Assert.Equal(dispose, h.Manager.IsDisposed);
        Assert.Equal(!dispose, h.Manager.Queue(a, Key()));
    }

    [Fact]
    public void MutationDuringCaptureCannotPublishAndReentrantPreparationIsNoOp()
    {
        using Harness h = new();
        var a = h.Register();
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.Null(h.Manager.BeginPreparation());
            Assert.False(h.Manager.Queue(a, Key()));
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.True(h.Manager.TryAllocate(request, out var candidate));
            a.Invalidate();
            Assert.False(candidate.Publish());
        }
        Assert.Equal(0, h.Manager.Statistics.Builds);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.True(h.Allocator.Targets[0].IsDisposed);
    }

    [Fact]
    public void PolicyMutationAndEndingScopeWhileCandidateActiveAreRejected()
    {
        using Harness h = new();
        var a = h.Register();
        Assert.True(h.Manager.Queue(a, Key()));
        var scope = h.Manager.BeginPreparation();
        Assert.Throws<InvalidOperationException>(() => h.Manager.MaximumEntries = 1);
        Assert.Throws<InvalidOperationException>(() => h.Manager.MaximumRetainedBytes = 1);
        Assert.Throws<InvalidOperationException>(() => h.Manager.MaximumTargetDimension = 1);
        Assert.True(h.Manager.TryDequeue(out var request));
        Assert.True(h.Manager.TryAllocate(request, out var candidate));
        Assert.Throws<InvalidOperationException>(() => scope.Dispose());
        Assert.False(h.Allocator.Targets[0].IsDisposed);
        candidate.Dispose(); scope.Dispose(); scope.Dispose();
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(64, h.Manager.MaximumEntries);
    }

    [Fact]
    public void AllocationAndFirstBindFailuresAreSuppressedUntilSizeDeviceOrPolicyChanges()
    {
        using Harness h = new();
        var a = h.Register();
        var error = new InvalidOperationException("native allocation diagnostic");
        h.Allocator.Failure = error;
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.False(h.Manager.TryAllocate(request, out _));
        }
        Assert.Same(error, h.Manager.LastNativeFailure);
        Assert.Equal(MGRenderCacheBypassReason.NativeFailure, h.Manager.Statistics.LastBypassReason);
        Assert.Equal(0, h.Manager.RetainedBytes);
        Assert.False(h.Manager.Queue(a, Key()));
        h.Manager.Clear();
        Assert.False(h.Manager.Queue(a, Key()));
        h.Allocator.Failure = null;
        h.Build(a, Key(2, 1));
        h.Manager.MaximumRetainedBytes++;
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.True(h.Manager.TryAllocate(request, out var candidate));
            candidate.RejectNativeFailure(error);
        }
        Assert.Equal(MGRenderCacheBypassReason.NativeFailure, h.Manager.Statistics.LastBypassReason);
        Assert.False(h.Manager.Queue(a, Key()));
        h.Manager.OnDeviceReset();
        h.Build(a);
        Assert.Null(h.Manager.LastNativeFailure);
        Assert.All(h.Allocator.Targets.Take(2), x => Assert.Equal(1, x.DisposeCalls));
    }

    [Fact]
    public void ReentrantClearDuringConstructionCancelsPublicationAndReleasesOnce()
    {
        using Harness h = new();
        var a = h.Register();
        h.Allocator.OnAllocate = h.Manager.Clear;
        Assert.True(h.Manager.Queue(a, Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.False(h.Manager.TryAllocate(request, out _));
        }
        Assert.Equal(1, h.Allocator.Targets[0].DisposeCalls);
        Assert.Equal(0, h.Manager.EntryCount);
    }

    [Fact]
    public void ClearResumesButDisposePermanentlyDisablesSameService()
    {
        using Harness h = new();
        var a = h.Register();
        h.Build(a); h.Manager.Clear(); h.Manager.Clear();
        Assert.Equal(1, h.Allocator.Targets[0].DisposeCalls);
        h.Build(a); h.Manager.Dispose(); h.Manager.Dispose(); h.Manager.Clear();
        Assert.True(h.Manager.IsDisposed);
        Assert.Equal(0, h.Manager.RetainedBytes);
        Assert.Equal(1, h.Allocator.Targets[1].DisposeCalls);
        Assert.False(h.Manager.Queue(a, Key()));
        Assert.Null(h.Manager.BeginPreparation());
    }

    [Fact]
    public void ResetInvalidatesReadyAndPendingAndAllowsNewDeviceGeneration()
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register();
        h.Build(a); Assert.True(h.Manager.Queue(b, Key()));
        h.Manager.OnDeviceReset();
        Assert.False(h.Manager.TryGetResource(a, Key(), out _));
        Assert.Equal(0, h.Manager.PendingCount);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.False(h.Manager.IsDisposed);
        h.Build(a);
        Assert.Equal(2, h.Manager.Statistics.Allocations);
        Assert.Equal(1, h.Manager.Statistics.Disposals);
    }

    [Fact]
    public void DeadOwnersAndOperationsAreNotRetainedByPendingReadyOrRegistrationTables()
    {
        using Harness h = new();
        var pendingOwner = AddCollectible(h, false, false);
        var readyOwner = AddCollectible(h, true, false);
        var operation = AddCollectible(h, true, true);
        Collect();
        Assert.False(pendingOwner.IsAlive); Assert.False(readyOwner.IsAlive); Assert.False(operation.IsAlive);
        using (h.Manager.BeginPreparation()) { Assert.False(h.Manager.TryDequeue(out _)); }
        Assert.Equal(0, h.Manager.PendingCount);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.All(h.Allocator.Targets, x => Assert.Equal(1, x.DisposeCalls));
    }

    [Fact]
    public void DisposedNativeResourceIsNeverReturnedAndIsRetiredAtSafePoint()
    {
        using Harness h = new();
        var a = h.Register(); h.Build(a);
        h.Allocator.Targets[0].ExternallyDisposed = true;
        Assert.False(h.Manager.TryGetResource(a, Key(), out _));
        Assert.Equal(1, h.Manager.EntryCount);
        using (h.Manager.BeginPreparation()) { Assert.Equal(0, h.Manager.EntryCount); }
    }

    [Fact]
    public void ReentrantLifecycleDuringTargetDisposalCannotDoubleReleaseOrPrepare()
    {
        using Harness h = new();
        h.Build(h.Register()); h.Build(h.Register());
        foreach (var target in h.Allocator.Targets)
        {
            target.OnDispose = () =>
            {
                Assert.Null(h.Manager.BeginPreparation());
                Assert.Throws<InvalidOperationException>(() => h.Manager.MaximumEntries = 1);
                h.Manager.Clear(); h.Manager.Dispose();
            };
        }
        h.Manager.Clear();
        Assert.All(h.Allocator.Targets, target => Assert.Equal(1, target.DisposeCalls));
        Assert.True(h.Manager.IsDisposed);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(0, h.Manager.RetainedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetirementDrainsLaterEntriesRetiredByEarliestDisposalCallback(bool dispose)
    {
        using Harness h = new();
        var a = h.Register(); var b = h.Register(); var c = h.Register();
        h.Build(a); h.Build(b); h.Build(c);
        a.Invalidate();
        h.Allocator.Targets[0].OnDispose = () =>
        {
            if (dispose) { h.Manager.Dispose(); } else { h.Manager.Clear(); }
        };
        using var scope = h.Manager.BeginPreparation();
        if (dispose)
        {
            Assert.Null(scope);
            Assert.False(h.Manager.IsPreparationActive);
        }
        else
        {
            Assert.NotNull(scope);
        }
        Assert.All(h.Allocator.Targets, target => Assert.Equal(1, target.DisposeCalls));
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(0, h.Manager.RetainedBytes);
        Assert.Equal(dispose, h.Manager.IsDisposed);
        h.Manager.Dispose();
        Assert.All(h.Allocator.Targets, target => Assert.Equal(1, target.DisposeCalls));
    }

    [Fact]
    public void ReentrantClearFromEvictionDisposalRejectsBeforeNewAllocation()
    {
        using Harness h = new();
        h.Manager.MaximumEntries = 1;
        h.Build(h.Register());
        h.Allocator.Targets[0].OnDispose = h.Manager.Clear;
        Assert.True(h.Manager.Queue(h.Register(), Key()));
        using (h.Manager.BeginPreparation())
        {
            Assert.True(h.Manager.TryDequeue(out var request));
            Assert.False(h.Manager.TryAllocate(request, out _));
        }
        Assert.Single(h.Allocator.Targets);
        Assert.Equal(0, h.Manager.EntryCount);
        Assert.Equal(0, h.Manager.RetainedBytes);
    }

    [Fact]
    public void DiagnosticsCountBypassReasonsWithoutFormatting()
    {
        using Harness h = new();
        var a = h.Register();
        Assert.False(h.Manager.Queue(a, Key(0, 1)));
        Assert.False(h.Manager.Queue(a, Key(0, 1)));
        Assert.False(h.Manager.Queue(a, Key(5000, 1)));
        Assert.Equal(2, h.Manager.GetBypassCount(MGRenderCacheBypassReason.InvalidSize));
        Assert.Equal(1, h.Manager.GetBypassCount(MGRenderCacheBypassReason.DimensionLimit));
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Manager.GetBypassCount((MGRenderCacheBypassReason)(-1)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddCollectible(Harness h, bool build, bool collectOperation)
    {
        object owner = new(), operation = new();
        var registration = h.Manager.Register(owner, operation);
        if (build) { h.Build(registration); } else { Assert.True(h.Manager.Queue(registration, Key())); }
        h.Identities.Add(collectOperation ? owner : operation);
        return new(collectOperation ? operation : owner);
    }
    private static void Collect()
    {
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    }
    private static MGRenderCacheKey Key(int width = 1, int height = 1) => MGRenderCacheKey.Create(width, height);

    private sealed class Harness : IDisposable
    {
        internal readonly FakeAllocator Allocator = new();
        internal readonly MGRenderCacheManager Manager;
        internal readonly List<object> Identities = new();
        internal Harness()
        {
            Manager = new(Allocator);
            Allocator.Manager = Manager;
        }
        internal MGRenderCacheRegistration Register()
        {
            object owner = new(), operation = new();
            Identities.Add(owner); Identities.Add(operation);
            return Manager.Register(owner, operation);
        }
        internal void Build(MGRenderCacheRegistration registration, MGRenderCacheKey? key = null)
        {
            Assert.True(Manager.Queue(registration, key ?? Key()));
            using (Manager.BeginPreparation())
            {
                Assert.True(Manager.TryDequeue(out var request));
                Assert.True(Manager.TryAllocate(request, out var candidate));
                Assert.True(candidate.Publish());
            }
        }
        public void Dispose() { Manager.Dispose(); GC.KeepAlive(Identities); }
    }
    private sealed class FakeAllocator : IMGRenderCacheAllocator
    {
        internal MGRenderCacheManager Manager = null!;
        internal readonly List<FakeTarget> Targets = new();
        internal readonly List<(long Bytes, int Entries)> Accounting = new();
        internal Exception? Failure;
        internal Action? OnAllocate;
        public IMGRenderCacheTarget Create(int width, int height)
        {
            Accounting.Add((Manager.RetainedBytes, Manager.EntryCount));
            Assert.InRange(Manager.RetainedBytes, 0, Manager.MaximumRetainedBytes);
            Assert.InRange(Manager.EntryCount, 1, Manager.MaximumEntries);
            if (Failure != null) { throw Failure; }
            FakeTarget target = new(); Targets.Add(target);
            OnAllocate?.Invoke();
            return target;
        }
    }
    private sealed class FakeTarget : IMGRenderCacheTarget
    {
        public RenderTarget2D Texture => null!;
        public bool IsDisposed => ExternallyDisposed || DisposeCalls > 0;
        internal bool ExternallyDisposed;
        internal int DisposeCalls;
        internal Action? OnDispose;
        public void Dispose() { DisposeCalls++; OnDispose?.Invoke(); }
    }
}
