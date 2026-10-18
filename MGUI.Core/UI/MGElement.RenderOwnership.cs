using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    public abstract partial class MGElement
    {
        private IReadOnlyList<MGElement> RetainedRenderPaintChildren = Array.Empty<MGElement>();
        private HashSet<MGElement> RenderPaintParents;

        /// <summary>Maintained, ordered, distinct contributing children. Reading this never discovers a subtree.</summary>
        internal IReadOnlyList<MGElement> RenderPaintChildren => RetainedRenderPaintChildren ?? Array.Empty<MGElement>();

        /// <summary>Membership/order hook for maintained eligibility contributions. Lists are immutable snapshots.
        /// Subscribers must detach when their registration ends; ordinary revision forwarding uses no subscriptions.</summary>
        internal event Action<MGElement, IReadOnlyList<MGElement>, IReadOnlyList<MGElement>, MGRenderMutationStamp> RenderPaintChildrenChanged;

        /// <summary>Extra children explicitly painted by built-in DrawSelf implementations, outside GetChildren.</summary>
        internal virtual IEnumerable<MGElement> GetAdditionalRenderPaintChildren() => Enumerable.Empty<MGElement>();

        private IEnumerable<MGElement> EnumerateRenderPaintChildren()
        {
            foreach (ComponentDrawPriority Priority in Enum.GetValues<ComponentDrawPriority>())
            {
                if (Priority == ComponentDrawPriority.BeforeContents)
                {
                    foreach (MGElement Child in GetAdditionalRenderPaintChildren())
                    {
                        yield return Child;
                    }
                }

                if (Components != null)
                {
                    foreach (MGComponentBase Component in Components)
                    {
                        if (Component.DrawPriority == Priority)
                        {
                            yield return Component.BaseElement;
                        }
                    }
                }

                if (Priority == ComponentDrawPriority.BeforeContents)
                {
                    foreach (MGElement Child in GetChildren())
                    {
                        yield return Child;
                    }
                }
            }
        }

        /// <summary>Reconciles changed membership, or explicitly notified raw/custom membership.
        /// Only structural changes and explicit invalidation call this; idle update/draw never does.</summary>
        internal void ReconcileRenderPaintChildren(bool IncludeDescendants = false)
        {
            ReconcileRenderPaintChildren(IncludeDescendants ? new HashSet<MGElement>(ReferenceEqualityComparer.Instance) : null);
        }

        private void ReconcileRenderPaintChildren(HashSet<MGElement> Visited)
        {
            if (Visited != null && !Visited.Add(this))
            {
                return;
            }

            List<MGElement> Children = new();
            HashSet<MGElement> Membership = new(ReferenceEqualityComparer.Instance);
            foreach (MGElement Child in EnumerateRenderPaintChildren())
            {

                // Only enumerated paint participants create edges; logical popup links are never enumerated.
                if (Child != null && Child != this && Membership.Add(Child))
                {
                    Children.Add(Child);
                }
            }

            IReadOnlyList<MGElement> Previous = RenderPaintChildren;
            bool Changed = Previous.Count != Children.Count;
            for (int Index = 0; !Changed && Index < Children.Count; ++Index)
            {
                Changed = !ReferenceEquals(Previous[Index], Children[Index]);
            }

            if (Changed)
            {
                foreach (MGElement Child in Previous)
                {
                    if (!Membership.Contains(Child))
                    {
                        Child.RenderPaintParents?.Remove(this);
                    }
                }

                HashSet<MGElement> OldMembership = new(Previous, ReferenceEqualityComparer.Instance);
                foreach (MGElement Child in Children)
                {
                    Child.RenderPaintParents ??= new(ReferenceEqualityComparer.Instance);
                    Child.RenderPaintParents.Add(this);
                    if (!OldMembership.Contains(Child))
                    {
                        Child.ReconcileRenderPaintChildren(Visited ?? new HashSet<MGElement>(ReferenceEqualityComparer.Instance) { this });
                    }
                }

                RetainedRenderPaintChildren = Children.AsReadOnly();
                ReconcileRenderEligibilityChildren(Previous, RetainedRenderPaintChildren);
                NotifyRenderContentMembershipChanged();
                MGRenderMutationStamp Stamp = MGRenderRevisionClock.NextMutation();
                Stamp = InvalidateRenderState(MGRenderInvalidationKind.Structure, Stamp);
                foreach (MGElement Child in Previous)
                {
                    if (!Membership.Contains(Child))
                    {
                        Child.InvalidateRemovedRenderSubtree(Stamp);
                    }
                }
                foreach (MGElement Child in Children)
                {
                    if (!OldMembership.Contains(Child))
                    {
                        if (IsRenderSourceSubtreeTrackingEnabled)
                        {
                            Child.EnableRenderSourceTracking();
                        }
                        Child.InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, Stamp);
                    }
                }
                RenderPaintChildrenChanged?.Invoke(this, Previous, RetainedRenderPaintChildren, Stamp);
            }

            if (Visited != null)
            {
                foreach (MGElement Child in Children)
                {
                    Child.ReconcileRenderPaintChildren(Visited);
                }
            }
        }

        private void ReconcileRenderParentAliases(MGElement Previous, MGElement Current)
        {
            Previous?.ReconcileRenderPaintChildren();
            if (!ReferenceEquals(Previous, Current))
            {
                Current?.ReconcileRenderPaintChildren();
            }
        }

        private void PropagateRenderInvalidation(MGRenderInvalidationKind Kind, MGRenderMutationStamp Stamp)
        {

            // Dependency rediscovery belongs to the original receiver, not each ancestor it dirties.
            Kind &= ~MGRenderInvalidationKind.Dependencies;
            if (Kind == MGRenderInvalidationKind.None || RenderPaintParents == null)
            {
                return;
            }

            foreach (MGElement Owner in RenderPaintParents.ToArray())
            {
                Owner.InvalidateRenderState(Kind, Stamp);
            }
        }
    }
}
