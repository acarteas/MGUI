using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    public abstract partial class MGElement
    {
        private MGRenderCacheSourceDependencies BaseRenderSources;
        internal bool IsRenderSourceSubtreeTrackingEnabled { get; private set; }

        internal MGRenderCacheSourceDependencies BaseRenderSourceDependencies
        {
            get
            {
                EnableRenderSourceTracking();
                return BaseRenderSources;
            }
        }

        /// <summary>Opts this contributing subtree into maintained source observations once.
        /// Revision/cache observation and cache registration use this seam; ordinary construction and painting do not.</summary>
        internal void EnableRenderSourceTracking()
        {
            if (!IsRenderSourceSubtreeTrackingEnabled)
            {
                EnableRenderSourceTracking(new(ReferenceEqualityComparer.Instance));
            }
        }

        private void EnableRenderSourceTracking(HashSet<MGElement> Ancestors)
        {
            if (IsRenderSourceSubtreeTrackingEnabled)
            {
                return;
            }

            IsRenderSourceSubtreeTrackingEnabled = true;
            EnableInheritedRenderSourceTracking(Ancestors);
            foreach (MGElement Child in RenderPaintChildren)
            {
                Child.EnableRenderSourceTracking(Ancestors);
            }
        }

        private void EnableInheritedRenderSourceTracking(HashSet<MGElement> Ancestors)
        {
            for (MGElement Ancestor = this; Ancestor != null && Ancestors.Add(Ancestor); Ancestor = Ancestor.Parent)
            {
                Ancestor.EnableLocalRenderSourceTracking();
            }
            SelfOrParentWindow?.EnableLocalRenderSourceTracking();
        }

        private void EnableLocalRenderSourceTracking()
        {
            if (BaseRenderSources == null)
            {
                BaseRenderSources = new(this, static Owner => ((MGElement)Owner).OnBaseRenderSourceChanged());
                RefreshBaseRenderSources();
                SelfOrParentWindow?.Desktop?.EnableRenderResourceTracking();
            }
        }

        /// <summary>Additional local paint inputs, discovered only on first observation, replacement or explicit invalidation.</summary>
        protected virtual IEnumerable<object> EnumerateControlRenderSources() => Enumerable.Empty<object>();

        internal void RefreshBaseRenderSources()
        {
            if (BaseRenderSources != null)
            {
                BaseRenderSources.ReplaceSources(new object[] { BackgroundBrush, OverlayBrush, DefaultTextForeground, (this as MGBorder)?.BorderBrush, (this as MGWindow)?.Theme }.Concat(EnumerateControlRenderSources()));
            }
        }

        private void OnBaseRenderSourceChanged()
        {
            InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, BaseRenderSources.LastNotificationStamp);
        }

        private void InvalidateRenderOwnershipContext()
        {
            if (BaseRenderSources != null)
            {
                EnableInheritedRenderSourceTracking(new(ReferenceEqualityComparer.Instance));
            }
            MGRenderMutationStamp Stamp = MGRenderRevisionClock.NextMutation();
            Stamp = InvalidateRenderState(MGRenderInvalidationKind.Structure, Stamp);
            InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, Stamp, new(ReferenceEqualityComparer.Instance));
        }

        private void InvalidateRenderContextAndLayout()
        {
            MGRenderMutationStamp Stamp = MGRenderRevisionClock.NextMutation();
            Stamp = InvalidateRenderState(MGRenderInvalidationKind.Layout, Stamp);
            InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, Stamp, new(ReferenceEqualityComparer.Instance));
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

        /// <summary>Invalidates inherited paint context only when a semantic input changes.
        /// Retained paint edges avoid discovery and a shared stamp coalesces aliases and ancestor forwarding.</summary>
        internal void InvalidateRenderContextSubtree(MGRenderInvalidationKind Kind)
            => InvalidateRenderContextSubtree(Kind, MGRenderRevisionClock.NextMutation());

        internal void InvalidateRenderContextSubtree(MGRenderInvalidationKind Kind, MGRenderMutationStamp Stamp)
        {
            InvalidateRenderContextSubtree(Kind, Stamp, new(ReferenceEqualityComparer.Instance));
        }

        internal void InvalidateRemovedRenderSubtree(MGRenderMutationStamp Stamp)
            => InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, Stamp,
                new(ReferenceEqualityComparer.Instance), true);

        private void InvalidateRenderContextSubtree(MGRenderInvalidationKind Kind, MGRenderMutationStamp Stamp,
            HashSet<MGElement> Visited, bool RetireContentObservations = false)
        {
            if (!Visited.Add(this))
            {
                return;
            }

            if (RetireContentObservations)
            {
                RenderContentBoundary?.MembershipChanged();
            }
            Stamp = InvalidateRenderState(Kind, Stamp);
            foreach (MGElement Child in RenderPaintChildren)
            {
                Child.InvalidateRenderContextSubtree(Kind, Stamp, Visited, RetireContentObservations);
            }
        }
    }
}
