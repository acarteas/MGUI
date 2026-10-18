using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MGUI.Core.UI
{
    /// <summary>Why a cache operation fell back to normal rendering.</summary>
    public enum MGRenderCacheBypassReason
    {
        None, Disabled, InvalidSize, DimensionLimit, Budget, QueueFull, Stale, OwnerUnavailable,
        NativeFailure, FailureSuppressed, ResourceDisposed, UnsupportedCallContext
    }

    /// <summary>Value-only lifecycle counters. Reading these counters does not allocate.</summary>
    public readonly record struct MGRenderCacheStatistics(long Hits, long Misses, long Builds, long RejectedRequests,
        long StaleRequests, long Evictions, long Allocations, long Disposals, long TargetSwitches,
        MGRenderCacheBypassReason LastBypassReason);

    // Keys contain values and an opaque clock token only: never owners, brushes, delegates or caller GPU resources.
    // RenderContext and LayoutResolution must change whenever any context not represented here changes.
    internal readonly record struct MGRenderCacheKey(object RevisionEpoch, ulong VisualRevision, ulong LayoutRevision,
        ulong StructureRevision, ulong SourceRevision, ulong Observation, ulong RenderContext, ulong LayoutResolution,
        Rectangle Bounds, Point Offset, int Width, int Height, int VisualState, float Opacity, Matrix Transform)
    {
        internal static MGRenderCacheKey Create(int width, int height) => new(MGRenderRevisionClock.CurrentEpoch,
            1, 1, 1, 1, 1, 1, 1, new Rectangle(0, 0, width, height), Point.Zero, width, height, 0, 1, Matrix.Identity);
    }

    // The allocation seam tests policy without inventing a GraphicsDevice. Native tests exercise the real implementation.
    internal interface IMGRenderCacheTarget : IDisposable
    {
        RenderTarget2D Texture { get; }
        bool IsDisposed { get; }
    }

    internal interface IMGRenderCacheAllocator
    {
        IMGRenderCacheTarget Create(int width, int height);
    }

    internal sealed class MGRenderCacheTarget : IMGRenderCacheTarget
    {
        public RenderTarget2D Texture { get; }
        public bool IsDisposed => Texture.IsDisposed;
        internal MGRenderCacheTarget(GraphicsDevice device, int width, int height)
        {
            Texture = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None,
                0, RenderTargetUsage.DiscardContents);
        }
        public void Dispose() => Texture.Dispose();
    }

    internal sealed class MGRenderCacheAllocator : IMGRenderCacheAllocator
    {
        private readonly GraphicsDevice Device;
        internal MGRenderCacheAllocator(GraphicsDevice device) { Device = device; }
        public IMGRenderCacheTarget Create(int width, int height) => new MGRenderCacheTarget(Device, width, height);
    }

    // Registrations do not own either identity. Producers retain their stable operation token for as long as it is attached.
    internal sealed class MGRenderCacheRegistration
    {
        private readonly WeakReference<MGRenderCacheManager> Manager;
        private readonly WeakReference<object> Owner;
        private readonly WeakReference<object> Operation;
        internal ulong Generation { get; private set; } = 1;
        internal MGRenderCacheKey? ObservedKey { get; private set; }
        internal MGRenderCacheRegistration(MGRenderCacheManager manager, object owner, object operation)
        {
            Manager = new(manager);
            Owner = new(owner);
            Operation = new(operation);
        }
        internal bool TryGetIdentities(out object owner, out object operation)
        {
            bool ownerAlive = Owner.TryGetTarget(out owner);
            bool operationAlive = Operation.TryGetTarget(out operation);
            return ownerAlive && operationAlive;
        }
        internal bool IsAlive => Owner.TryGetTarget(out _) && Operation.TryGetTarget(out _);
        internal bool BelongsTo(MGRenderCacheManager manager) => Manager.TryGetTarget(out var target) && ReferenceEquals(target, manager);
        internal void Observe(MGRenderCacheKey key) { ObservedKey = key; }

        /// <summary>Call before direct paint, on detach/disable, and on any semantic invalidation. Never disposes GPU objects.</summary>
        internal void Invalidate()
        {
            ObservedKey = null;
            if (Generation == ulong.MaxValue)
            {
                if (Manager.TryGetTarget(out var manager))
                {
                    manager.InvalidateGeneration();
                }
                Generation = 1;
            }
            else
            {
                Generation++;
            }
            if (Manager.TryGetTarget(out var cacheManager))
            {
                cacheManager.Retire(this);
            }
        }
    }

    internal readonly record struct MGRenderCacheRequest(MGRenderCacheRegistration Registration, MGRenderCacheKey Key,
        object ServiceGeneration, ulong RegistrationGeneration);

    // The candidate is a lease on an accounted target. End/flush the transaction and unbind BEFORE Publish/Dispose/Reject.
    // The surrounding preparation scope must outlive all candidate leases.
    internal sealed class MGRenderCacheCandidate : IDisposable
    {
        private MGRenderCacheManager Manager;
        internal MGRenderCacheManager.Entry Entry { get; }
        internal MGRenderCacheCandidate(MGRenderCacheManager manager, MGRenderCacheManager.Entry entry)
        {
            Manager = manager;
            Entry = entry;
        }
        internal RenderTarget2D Target => Entry.Resource?.Texture;
        internal bool Publish()
        {
            var manager = Manager;
            Manager = null;
            return manager != null && manager.CompleteCandidate(Entry, true);
        }
        internal void RejectNativeFailure(Exception error)
        {
            var manager = Manager;
            Manager = null;
            if (manager != null)
            {
                manager.CompleteCandidate(Entry, false, MGRenderCacheBypassReason.None);
                manager.RecordNativeFailure(Entry.Request.Key.Width, Entry.Request.Key.Height, error);
            }
        }
        public void Dispose()
        {
            var manager = Manager;
            Manager = null;
            manager?.CompleteCandidate(Entry, false);
        }
    }
}
