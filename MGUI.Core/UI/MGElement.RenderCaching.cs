using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    [Flags]
    internal enum MGRenderInvalidationKind
    {
        None = 0,
        Visual = 1,
        Layout = 2,
        Structure = 4,
        Dependencies = 8
    }

    internal readonly record struct MGRenderMutationStamp(object Epoch, ulong Sequence);

    /// <summary>UI-thread clock shared by immutable cache observations. An epoch is compared by identity.</summary>
    internal static class MGRenderRevisionClock
    {
        internal static object CurrentEpoch { get; private set; } = new();
        private static ulong Sequence;

        internal static MGRenderMutationStamp NextMutation()
        {
            if (Sequence == ulong.MaxValue)
            {
                RestartEpoch();
            }

            return new(CurrentEpoch, ++Sequence);
        }

        internal static void RestartEpoch()
        {
            CurrentEpoch = new();
            Sequence = 0;
        }
    }

    internal readonly record struct MGRenderRevisionSnapshot(object Epoch, ulong Visual, ulong Layout, ulong Structure);

    public abstract partial class MGElement
    {
        private object RenderRevisionEpoch;
        private (MGElement Source, MGRenderMutationStamp Stamp)? ForwardedLayoutMutation;
        private ulong CurrentVisualRevision;
        private ulong CurrentLayoutRevision;
        private ulong CurrentStructureRevision;
        private ulong LastVisualMutation;
        private ulong LastLayoutMutation;
        private ulong LastStructureMutation;
        private ulong LastDependencyMutation;

        /// <summary>Revision of this element's rendered appearance, starting at 1 within a cache epoch.</summary>
        public ulong VisualRevision
        {
            get
            {
                EnableRenderSourceTracking();
                EnsureRenderRevisionEpoch();
                return CurrentVisualRevision;
            }
        }

        /// <summary>Revision of layout-affecting state, starting at 1 within a cache epoch.</summary>
        public ulong LayoutRevision
        {
            get
            {
                EnsureRenderRevisionEpoch();
                return CurrentLayoutRevision;
            }
        }

        /// <summary>Revision of paint membership and order, starting at 1 within a cache epoch.</summary>
        public ulong StructureRevision
        {
            get
            {
                EnsureRenderRevisionEpoch();
                return CurrentStructureRevision;
            }
        }

        /// <summary>Notifies rendering that custom drawing or an externally mutated resource changed.
        /// Reconciles the receiver's contributing subtree, including explicitly changed raw component lists.
        /// Does not invalidate layout or cached measurements.</summary>
        public void InvalidateVisual()
        {
            ReconcileRenderPaintChildren(true);
            ReconcileExplicitRenderSources(new(ReferenceEqualityComparer.Instance), MGRenderRevisionClock.NextMutation());
        }

        private void ReconcileExplicitRenderSources(HashSet<MGElement> Visited, MGRenderMutationStamp Stamp)
        {
            if (!Visited.Add(this))
            {
                return;
            }

            OnVisualInvalidated();
            RefreshBaseRenderSources();
            Stamp = InvalidateRenderState(MGRenderInvalidationKind.Visual | MGRenderInvalidationKind.Dependencies, Stamp);
            foreach (MGElement Child in RenderPaintChildren)
            {
                Child.ReconcileExplicitRenderSources(Visited, Stamp);
            }
        }

        /// <summary>Refreshes derived paint data after explicit opaque mutations, without invalidating measurement.</summary>
        protected virtual void OnVisualInvalidated() { }

        /// <summary>Semantic invalidation hook for maintained paint ownership and dependency registrations.
        /// Dependencies requests reconciliation even when no observable resource event was available.</summary>
        internal event Action<MGElement, MGRenderInvalidationKind, MGRenderMutationStamp> RenderInvalidated;

        internal MGRenderRevisionSnapshot GetRenderRevisionSnapshot()
        {
            EnableRenderSourceTracking();
            EnsureRenderRevisionEpoch();
            return new(RenderRevisionEpoch, CurrentVisualRevision, CurrentLayoutRevision, CurrentStructureRevision);
        }

        /// <summary>Publication/acknowledgement can accept only the exact state it captured.
        /// This never clears a newer mutation or changes layout validity.</summary>
        internal bool IsRenderRevisionCurrent(MGRenderRevisionSnapshot Snapshot)
            => ReferenceEquals(Snapshot.Epoch, MGRenderRevisionClock.CurrentEpoch) && Snapshot == GetRenderRevisionSnapshot();

        private void EnsureRenderRevisionEpoch()
        {
            if (!ReferenceEquals(RenderRevisionEpoch, MGRenderRevisionClock.CurrentEpoch))
            {
                RenderRevisionEpoch = MGRenderRevisionClock.CurrentEpoch;
                CurrentVisualRevision = 1;
                CurrentLayoutRevision = 1;
                CurrentStructureRevision = 1;
                LastVisualMutation = 0;
                LastLayoutMutation = 0;
                LastStructureMutation = 0;
                LastDependencyMutation = 0;
            }
        }

        /// <summary>Preserves normal measurement invalidation while reusing an observable resource mutation.</summary>
        internal void LayoutChangedForRenderMutation(MGRenderMutationStamp Stamp)
        {
            Stamp = InvalidateRenderState(MGRenderInvalidationKind.Layout, Stamp);
            var Previous = ForwardedLayoutMutation;
            ForwardedLayoutMutation = (this, Stamp);
            try
            {
                LayoutChanged(this, true);
            }
            finally
            {
                ForwardedLayoutMutation = Previous;
            }
        }

        private (MGRenderMutationStamp Stamp, bool Forwarded) TakeLayoutMutation(MGElement Source)
        {
            if (ForwardedLayoutMutation is { } Forwarded && ReferenceEquals(Forwarded.Source, Source))
            {
                ForwardedLayoutMutation = null;
                return (Forwarded.Stamp, true);
            }

            return (MGRenderRevisionClock.NextMutation(), false);
        }

        private void ForwardLayoutChangeToParent(MGElement Source, bool NotifyParent, MGRenderMutationStamp Stamp)
        {
            MGElement LayoutParent = Parent;
            if (LayoutParent == null)
            {
                return;
            }

            var Previous = LayoutParent.ForwardedLayoutMutation;
            LayoutParent.ForwardedLayoutMutation = (Source, Stamp);
            try
            {
                LayoutParent.LayoutChanged(Source, NotifyParent);
            }
            finally
            {
                LayoutParent.ForwardedLayoutMutation = Previous;
            }
        }

        internal void InvalidateRenderState(MGRenderInvalidationKind Kind)
        {
            if (Kind != MGRenderInvalidationKind.None)
            {
                InvalidateRenderState(Kind, MGRenderRevisionClock.NextMutation());
            }
        }

        /// <summary>Reuses a mutation stamp when ownership aliases forward the same change.
        /// Independent later changes always receive a new stamp, including while a capture is in progress.</summary>
        internal MGRenderMutationStamp InvalidateRenderState(MGRenderInvalidationKind Kind, MGRenderMutationStamp Stamp)
        {
            if (Kind == MGRenderInvalidationKind.None)
            {
                return Stamp;
            }

            RefreshLocalRenderEligibility();

            if ((Kind & (MGRenderInvalidationKind.Layout | MGRenderInvalidationKind.Structure)) != 0)
            {
                Kind |= MGRenderInvalidationKind.Visual;
            }

            EnsureRenderRevisionEpoch();
            if (!ReferenceEquals(Stamp.Epoch, RenderRevisionEpoch))
            {
                Stamp = MGRenderRevisionClock.NextMutation();
            }

            MGRenderInvalidationKind Changed = MGRenderInvalidationKind.None;
            if ((Kind & MGRenderInvalidationKind.Visual) != 0 && Stamp.Sequence > LastVisualMutation)
            {
                Changed |= MGRenderInvalidationKind.Visual;
            }

            if ((Kind & MGRenderInvalidationKind.Layout) != 0 && Stamp.Sequence > LastLayoutMutation)
            {
                Changed |= MGRenderInvalidationKind.Layout;
            }

            if ((Kind & MGRenderInvalidationKind.Structure) != 0 && Stamp.Sequence > LastStructureMutation)
            {
                Changed |= MGRenderInvalidationKind.Structure;
            }

            if ((Kind & MGRenderInvalidationKind.Dependencies) != 0 && Stamp.Sequence > LastDependencyMutation)
            {
                Changed |= MGRenderInvalidationKind.Dependencies;
            }

            if (((Changed & MGRenderInvalidationKind.Visual) != 0 && CurrentVisualRevision == ulong.MaxValue) ||
                ((Changed & MGRenderInvalidationKind.Layout) != 0 && CurrentLayoutRevision == ulong.MaxValue) ||
                ((Changed & MGRenderInvalidationKind.Structure) != 0 && CurrentStructureRevision == ulong.MaxValue))
            {
                MGRenderRevisionClock.RestartEpoch();
                EnsureRenderRevisionEpoch();
                Stamp = MGRenderRevisionClock.NextMutation();
            }

            if ((Changed & MGRenderInvalidationKind.Visual) != 0)
            {
                ++CurrentVisualRevision;
                LastVisualMutation = Stamp.Sequence;
            }

            if ((Changed & MGRenderInvalidationKind.Layout) != 0)
            {
                ++CurrentLayoutRevision;
                LastLayoutMutation = Stamp.Sequence;
            }

            if ((Changed & MGRenderInvalidationKind.Structure) != 0)
            {
                ++CurrentStructureRevision;
                LastStructureMutation = Stamp.Sequence;
            }

            if ((Changed & MGRenderInvalidationKind.Dependencies) != 0)
            {
                LastDependencyMutation = Stamp.Sequence;
            }

            if (Changed != MGRenderInvalidationKind.None)
            {
                PropagateRenderInvalidation(Changed, Stamp);
                RenderInvalidated?.Invoke(this, Changed, Stamp);
            }

            return Stamp;
        }
    }
}
