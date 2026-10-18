using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI
{
    /// <summary>
    /// Desktop-owned, bounded render-target storage. Targets are allocated only by explicit cache preparation.
    /// All calls belong on the UI/render thread. Clear, Dispose and policy changes require a transaction-free safe point;
    /// reentrant Clear/Dispose during preparation invalidate immediately and defer destruction until preparation ends.
    /// Caller textures, effects, fonts and the renderer/device are never owned by this service.
    /// </summary>
    public sealed class MGRenderCacheManager : IDisposable
    {
        private readonly IMGRenderCacheAllocator Allocator;
        private readonly DeviceForwarder DeviceEvents;
        private readonly ConditionalWeakTable<object, ConditionalWeakTable<object, MGRenderCacheRegistration>> Registrations = new();
        private readonly List<MGRenderCacheRequest> Pending = new();
        private readonly List<Entry> Entries = new();
        private readonly Dictionary<MGRenderCacheRegistration, Entry> Ready = new();
        private readonly List<(int Width, int Height)> FailedSizes = new();
        private object ServiceGeneration = new();
        private object RevisionEpoch = MGRenderRevisionClock.CurrentEpoch;
        private long Sequence;
        private long UseSequence;
        private bool IsPreparing;
        private bool IsDraining;
        private readonly long[] Bypasses = new long[(int)MGRenderCacheBypassReason.UnsupportedCallContext + 1];
        private Entry ActiveCandidate;
        private long Hits, Misses, Builds, Rejections, Stale, Evictions, Allocations, Disposals, Switches;
        private MGRenderCacheBypassReason LastBypass;
        private long ByteLimit = 33_554_432;
        private int EntryLimit = 64;
        private int DimensionLimit = 4096;

        /// <summary>Every live manager target, including retired and in-flight targets, is charged here.</summary>
        public long RetainedBytes { get; private set; }
        /// <summary>Includes retired and in-flight targets until they have actually been disposed.</summary>
        public int EntryCount => Entries.Count;
        public bool IsDisposed { get; private set; }
        public MGRenderCacheStatistics Statistics => new(Hits, Misses, Builds, Rejections, Stale, Evictions,
            Allocations, Disposals, Switches, LastBypass);
        /// <summary>The actual exception from the latest native construction or first-bind/capture failure.</summary>
        public Exception LastNativeFailure { get; private set; }
        /// <summary>Number of direct-render fallbacks classified by reason; no formatting or string allocation is involved.</summary>
        public long GetBypassCount(MGRenderCacheBypassReason reason)
        {
            int index = (int)reason;
            if (index < 0 || index >= Bypasses.Length) { throw new ArgumentOutOfRangeException(nameof(reason)); }
            return Bypasses[index];
        }
        internal int PendingCount => Pending.Count;
        internal bool IsPreparationActive => IsPreparing;

        public long MaximumRetainedBytes
        {
            get => ByteLimit;
            set
            {
                if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
                EnsurePolicySafePoint();
                if (ByteLimit != value)
                {
                    ByteLimit = value;
                    PolicyChanged();
                }
            }
        }
        public int MaximumEntries
        {
            get => EntryLimit;
            set
            {
                if (value < 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
                EnsurePolicySafePoint();
                if (EntryLimit != value)
                {
                    EntryLimit = value;
                    PolicyChanged();
                }
            }
        }
        /// <summary>Allocation policy cap, not a claimed hardware limit. Native creation and first bind remain authoritative.</summary>
        public int MaximumTargetDimension
        {
            get => DimensionLimit;
            set
            {
                if (value <= 0) { throw new ArgumentOutOfRangeException(nameof(value)); }
                EnsurePolicySafePoint();
                if (DimensionLimit != value)
                {
                    DimensionLimit = value;
                    PolicyChanged();
                }
            }
        }

        internal MGRenderCacheManager(GraphicsDevice device) : this(new MGRenderCacheAllocator(device ?? throw new ArgumentNullException(nameof(device))))
        {
            DeviceEvents = new(this, device);
            if (device.IsDisposed)
            {
                Dispose();
            }
        }
        internal MGRenderCacheManager(IMGRenderCacheAllocator allocator)
        {
            Allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        }

        internal MGRenderCacheRegistration Register(object owner, object operation)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(operation);
            var operations = Registrations.GetValue(owner, _ => new());
            if (!operations.TryGetValue(operation, out var registration))
            {
                registration = new(this, owner, operation);
                operations.Add(operation, registration);
            }
            return registration;
        }

        /// <summary>
        /// Only call after successful normal paint with a still-current immutable observation. The newest request replaces
        /// the prior request for that owner/operation in-place. No delegates or strong owner/resource references are stored.
        /// </summary>
        internal bool Queue(MGRenderCacheRegistration registration, MGRenderCacheKey key)
        {
            CheckRevisionEpoch();
            if (IsPreparing || IsDisposed || ByteLimit == 0 || EntryLimit == 0)
            {
                return Reject(MGRenderCacheBypassReason.Disabled);
            }
            if (!registration.BelongsTo(this) || !registration.IsAlive)
            {
                return Reject(MGRenderCacheBypassReason.OwnerUnavailable);
            }
            if (!ReferenceEquals(key.RevisionEpoch, RevisionEpoch))
            {
                return RejectStale();
            }
            if (!ValidateSize(key, out _)) { return false; }
            if (IsFailureSuppressed(key)) { return Reject(MGRenderCacheBypassReason.FailureSuppressed); }
            if (registration.ObservedKey != key)
            {
                RetireEntries(registration);
            }
            registration.Observe(key);
            var request = new MGRenderCacheRequest(registration, key, ServiceGeneration, registration.Generation);
            for (int i = 0; i < Pending.Count; i++)
            {
                if (ReferenceEquals(Pending[i].Registration, registration))
                {
                    Pending[i] = request;
                    return true;
                }
            }
            SweepDeadPending();
            if (Pending.Count >= EntryLimit)
            {
                return Reject(MGRenderCacheBypassReason.QueueFull);
            }
            Pending.Add(request);
            return true;
        }

        /// <summary>Lookup never allocates, binds, or disposes. The frame protocol/eligibility belongs to the producer.</summary>
        internal bool TryGet(MGRenderCacheRegistration registration, MGRenderCacheKey key, out RenderTarget2D target)
        {
            bool found = TryGetResource(registration, key, out var resource);
            target = resource?.Texture;
            return found;
        }
        internal bool TryGetResource(MGRenderCacheRegistration registration, MGRenderCacheKey key, out IMGRenderCacheTarget resource)
        {
            CheckRevisionEpoch();
            resource = null;
            if (!IsDisposed && !IsPreparing && registration.BelongsTo(this) && registration.IsAlive
                && registration.ObservedKey == key && ReferenceEquals(key.RevisionEpoch, RevisionEpoch))
            {
                if (Ready.TryGetValue(registration, out var entry) && !entry.Retired && entry.Ready
                    && entry.Request.Key == key && IsCurrent(entry.Request))
                {
                    if (entry.Resource.IsDisposed)
                    {
                        entry.Retired = true;
                        Ready.Remove(registration);
                        Reject(MGRenderCacheBypassReason.ResourceDisposed);
                    }
                    else
                    {
                        entry.LastUse = NextUse();
                        Hits++;
                        resource = entry.Resource;
                        return true;
                    }
                }
            }
            Misses++;
            return false;
        }

        /// <summary>
        /// Enter only outside all external batches/transactions, before scene rendering. A null scope means disposed or
        /// reentrant preparation. End every capture transaction and unbind its target before releasing its candidate,
        /// then dispose this scope. No device-state management or painting is performed by this resource layer.
        /// </summary>
        internal IDisposable BeginPreparation()
        {
            if (IsDisposed || IsPreparing || IsDraining) { return null; }
            CheckRevisionEpoch();
            SweepDeadEntries();
            DrainRetired();
            if (IsDisposed) { return null; }
            SweepDeadPending();
            IsPreparing = true;
            return new PreparationScope(this);
        }
        internal bool TryDequeue(out MGRenderCacheRequest request)
        {
            RequirePreparation();
            while (Pending.Count != 0)
            {
                request = Pending[0];
                Pending.RemoveAt(0);
                if (IsCurrent(request)) { return true; }
                RejectStale();
            }
            request = default;
            return false;
        }

        /// <summary>Validate producer state before this call AND immediately before painting. A false result means direct fallback.</summary>
        internal bool TryAllocate(MGRenderCacheRequest request, out MGRenderCacheCandidate candidate)
        {
            RequirePreparation();
            candidate = null;
            if (ActiveCandidate != null) { throw new InvalidOperationException("Release the active capture before allocating another target."); }
            CheckRevisionEpoch();
            if (!IsCurrent(request)) { return RejectStale(); }
            if (!ValidateSize(request.Key, out long bytes)) { return false; }
            if (IsFailureSuppressed(request.Key)) { return Reject(MGRenderCacheBypassReason.FailureSuppressed); }

            // Replacements must release their old target before the new allocation. This is a transaction-free point.
            RetireEntries(request.Registration);
            DrainRetired();
            while (EntryCount >= EntryLimit || RetainedBytes > ByteLimit - bytes)
            {
                Entry oldest = FindOldest();
                if (oldest == null) { return Reject(MGRenderCacheBypassReason.Budget); }
                oldest.Retired = true;
                Evictions++;
                DrainRetired();
            }
            if (!IsCurrent(request)) { return RejectStale(); }
            var entry = new Entry(request, bytes, NextSequence());
            Entries.Add(entry);
            RetainedBytes += bytes;
            ActiveCandidate = entry;
            try
            {
                entry.Resource = Allocator.Create(request.Key.Width, request.Key.Height);
                if (entry.Resource == null) { throw new InvalidOperationException("The target allocator returned null."); }
                Allocations++;
            }
            catch (Exception error)
            {
                CompleteCandidate(entry, false, MGRenderCacheBypassReason.None);
                RecordNativeFailure(request.Key.Width, request.Key.Height, error);
                return false;
            }
            if (!IsCurrent(request) || entry.Retired || entry.Resource.IsDisposed)
            {
                CompleteCandidate(entry, false);
                return false;
            }
            candidate = new(this, entry);
            return true;
        }

        internal bool IsCurrent(MGRenderCacheRequest request) => !IsDisposed
            && ReferenceEquals(request.ServiceGeneration, ServiceGeneration)
            && ReferenceEquals(request.Key.RevisionEpoch, MGRenderRevisionClock.CurrentEpoch)
            && request.Registration.BelongsTo(this) && request.Registration.IsAlive
            && request.Registration.Generation == request.RegistrationGeneration
            && request.Registration.ObservedKey == request.Key;

        internal bool CompleteCandidate(Entry entry, bool publish, MGRenderCacheBypassReason rejection = MGRenderCacheBypassReason.Stale)
        {
            if (!ReferenceEquals(ActiveCandidate, entry)) { throw new InvalidOperationException("The capture lease is not active."); }
            bool ready = publish && !entry.Retired && IsCurrent(entry.Request) && entry.Resource != null && !entry.Resource.IsDisposed;
            entry.Ready = ready;
            entry.Retired = !ready;
            ActiveCandidate = null;
            if (ready)
            {
                Ready[entry.Request.Registration] = entry;
                Builds++;
            }
            else if (rejection == MGRenderCacheBypassReason.Stale)
            {
                RejectStale();
            }
            else if (rejection != MGRenderCacheBypassReason.None)
            {
                Reject(rejection);
            }

            // The caller has ended the capture transaction and unbound. Retired targets can now be destroyed safely.
            DrainRetired();
            return ready && !entry.Retired && IsCurrent(entry.Request);
        }
        internal void RecordNativeFailure(int width, int height, Exception error)
        {
            LastNativeFailure = error;
            if (!FailedSizes.Contains((width, height)))
            {
                if (FailedSizes.Count >= Math.Max(1, EntryLimit)) { FailedSizes.RemoveAt(0); }
                FailedSizes.Add((width, height));
            }
            Reject(MGRenderCacheBypassReason.NativeFailure);
        }
        internal void RecordTargetSwitch() { Switches++; }
        internal void RecordBypass(MGRenderCacheBypassReason reason) { Reject(reason); }

        /// <summary>Logical retirement only; safe during final composition with deferred sprites.</summary>
        private void RetireEntries(MGRenderCacheRegistration registration)
        {
            Ready.Remove(registration);
            foreach (var entry in Entries)
            {
                if (ReferenceEquals(entry.Request.Registration, registration)) { entry.Retired = true; }
            }
        }
        internal void Retire(MGRenderCacheRegistration registration)
        {
            RetireEntries(registration);
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Pending[i].Registration, registration)) { Pending.RemoveAt(i); }
            }
        }
        internal void InvalidateGeneration()
        {
            ServiceGeneration = new();
            Pending.Clear();
            Ready.Clear();
            foreach (var entry in Entries) { entry.Retired = true; }
        }
        private void CheckRevisionEpoch()
        {
            object current = MGRenderRevisionClock.CurrentEpoch;
            if (!ReferenceEquals(current, RevisionEpoch))
            {
                RevisionEpoch = current;
                InvalidateGeneration();
            }
        }

        /// <summary>Clear at a transaction-free point. During a capture this cancels immediately and defers GPU disposal.</summary>
        public void Clear()
        {
            InvalidateGeneration();
            if (!IsPreparing) { DrainRetired(); }
        }
        /// <summary>Permanently disables this service. Idempotent; accessing desktop.RenderCaches never resurrects it.</summary>
        public void Dispose()
        {
            if (IsDisposed) { return; }
            IsDisposed = true;
            DeviceEvents?.Dispose();
            Clear();
        }
        internal void OnDeviceReset()
        {
            FailedSizes.Clear();
            LastNativeFailure = null;
            Clear();
        }

        private void EnsurePolicySafePoint()
        {
            if (IsPreparing || IsDraining) { throw new InvalidOperationException("Cache policy cannot change during preparation or resource retirement."); }
        }
        private void PolicyChanged()
        {
            SweepDeadEntries();
            ServiceGeneration = new();
            Pending.Clear();
            FailedSizes.Clear();
            LastNativeFailure = null;
            foreach (var entry in Entries)
            {
                if (ByteLimit == 0 || EntryLimit == 0 || entry.Request.Key.Width > DimensionLimit || entry.Request.Key.Height > DimensionLimit)
                {
                    entry.Retired = true;
                }
                else
                {
                    entry.Request = entry.Request with { ServiceGeneration = ServiceGeneration };
                }
            }
            DrainRetired();
            while (EntryCount > EntryLimit || RetainedBytes > ByteLimit)
            {
                var oldest = FindOldest();
                oldest.Retired = true;
                Evictions++;
                DrainRetired();
            }
        }
        private bool ValidateSize(MGRenderCacheKey key, out long bytes)
        {
            bytes = 0;
            if (key.Width <= 0 || key.Height <= 0) { return Reject(MGRenderCacheBypassReason.InvalidSize); }
            if (key.Width > DimensionLimit || key.Height > DimensionLimit) { return Reject(MGRenderCacheBypassReason.DimensionLimit); }
            try { bytes = checked((long)key.Width * key.Height * 4); }
            catch (OverflowException) { return Reject(MGRenderCacheBypassReason.Budget); }
            if (EntryLimit == 0 || bytes > ByteLimit) { return Reject(MGRenderCacheBypassReason.Budget); }
            return true;
        }
        private bool IsFailureSuppressed(MGRenderCacheKey key) => FailedSizes.Contains((key.Width, key.Height));
        private bool Reject(MGRenderCacheBypassReason reason)
        {
            Rejections++;
            LastBypass = reason;
            Bypasses[(int)reason]++;
            return false;
        }
        private bool RejectStale()
        {
            Stale++;
            return Reject(MGRenderCacheBypassReason.Stale);
        }
        private Entry FindOldest()
        {
            Entry result = null;
            foreach (var entry in Entries)
            {
                if (entry == ActiveCandidate) { continue; }
                if (result == null || entry.LastUse < result.LastUse || (entry.LastUse == result.LastUse && entry.Sequence < result.Sequence))
                {
                    result = entry;
                }
            }
            return result;
        }
        private long NextSequence()
        {
            if (Sequence == long.MaxValue)
            {
                Entries.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                long next = 0;
                foreach (var entry in Entries) { entry.Sequence = ++next; }
                Sequence = next;
            }
            return ++Sequence;
        }
        private long NextUse()
        {
            if (UseSequence == long.MaxValue)
            {
                Entries.Sort((a, b) => a.LastUse != b.LastUse ? a.LastUse.CompareTo(b.LastUse) : a.Sequence.CompareTo(b.Sequence));
                long next = 0;
                foreach (var entry in Entries) { entry.LastUse = entry.LastUse == 0 ? 0 : ++next; }
                UseSequence = next;
            }
            return ++UseSequence;
        }
        private void SweepDeadPending()
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (!IsCurrent(Pending[i]))
                {
                    Pending.RemoveAt(i);
                    RejectStale();
                }
            }
        }
        private void SweepDeadEntries()
        {
            foreach (var entry in Entries)
            {
                if (!IsCurrent(entry.Request) || entry.Resource?.IsDisposed == true) { entry.Retired = true; }
            }
        }
        private void DrainRetired()
        {
            if (ActiveCandidate != null || IsDraining) { return; }
            IsDraining = true;
            try
            {
                while (true)
                {
                    int i = Entries.Count - 1;
                    while (i >= 0 && !Entries[i].Retired) { i--; }
                    if (i < 0) { break; }
                    var entry = Entries[i];

                    // Disposal callbacks may retire entries already inspected. Re-select after each release until none remain.
                    if (Ready.TryGetValue(entry.Request.Registration, out var ready) && ReferenceEquals(ready, entry))
                    {
                        Ready.Remove(entry.Request.Registration);
                    }
                    try
                    {
                        entry.Resource?.Dispose();
                    }
                    finally
                    {
                        if (entry.Resource != null) { Disposals++; }
                        RetainedBytes -= entry.Bytes;
                        Entries.RemoveAt(i);
                    }
                }
            }
            finally
            {
                IsDraining = false;
            }
        }
        private void RequirePreparation()
        {
            if (!IsPreparing) { throw new InvalidOperationException("Target work requires a preparation scope."); }
        }
        private void EndPreparation()
        {
            if (ActiveCandidate != null)
            {
                throw new InvalidOperationException("End the transaction, unbind and release the candidate before ending preparation.");
            }
            IsPreparing = false;
            SweepDeadEntries();
            DrainRetired();
        }
        private sealed class PreparationScope : IDisposable
        {
            private MGRenderCacheManager Manager;
            internal PreparationScope(MGRenderCacheManager manager) { Manager = manager; }
            public void Dispose()
            {
                if (Manager != null)
                {
                    Manager.EndPreparation();
                    Manager = null;
                }
            }
        }
        internal sealed class Entry
        {
            internal MGRenderCacheRequest Request;
            internal readonly long Bytes;
            internal long Sequence, LastUse;
            internal IMGRenderCacheTarget Resource;
            internal bool Ready, Retired;
            internal Entry(MGRenderCacheRequest request, long bytes, long sequence)
            {
                Request = request;
                Bytes = bytes;
                Sequence = sequence;
            }
        }

        // A live device owns only this forwarder. Its weak target never roots an abandoned manager or desktop.
        private sealed class DeviceForwarder : IDisposable
        {
            private readonly WeakReference<MGRenderCacheManager> Manager;
            private GraphicsDevice Device;
            internal DeviceForwarder(MGRenderCacheManager manager, GraphicsDevice device)
            {
                Manager = new(manager);
                Device = device;
                device.DeviceResetting += Reset;
                device.DeviceReset += Reset;
                device.Disposing += Disposing;
            }
            private void Reset(object sender, EventArgs e)
            {
                if (Manager.TryGetTarget(out var manager)) { manager.OnDeviceReset(); }
                else { Dispose(); }
            }
            private void Disposing(object sender, EventArgs e)
            {
                if (Manager.TryGetTarget(out var manager)) { manager.Dispose(); }
                Dispose();
            }
            public void Dispose()
            {
                if (Device != null)
                {
                    Device.DeviceResetting -= Reset;
                    Device.DeviceReset -= Reset;
                    Device.Disposing -= Disposing;
                    Device = null;
                }
            }
        }
    }
}
