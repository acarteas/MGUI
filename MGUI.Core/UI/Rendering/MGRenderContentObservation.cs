using MGUI.Core.UI.Containers;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    /// <summary>Change-time registration over captured paint edges. Publishers hold only weak observers.</summary>
    internal sealed class MGRenderContentObservation
    {
        private readonly MGRenderCache Boundary;
        private readonly HashSet<MGElement> Publishers = new(ReferenceEqualityComparer.Instance);
        private bool CurrentContentCompleted;
        internal int DirectPaintDepth;
        internal int PublisherCount => Publishers.Count;

        internal MGRenderContentObservation(MGRenderCache boundary) { Boundary = boundary; }

        internal void EnsureRegistered()
        {
            if (Publishers.Count == 0 && Boundary.Content != null)
            {
                Register(Boundary.Content);
            }
        }

        private void Register(MGElement element)
        {
            if (!Publishers.Add(element))
            {
                return;
            }
            element.AddRenderContentObserver(this);
            foreach (MGElement child in element.RenderPaintChildren)
            {
                Register(child);
            }
        }

        internal void Clear()
        {
            foreach (MGElement publisher in Publishers)
            {
                publisher.RemoveRenderContentObserver(this);
            }
            Publishers.Clear();
        }

        internal void MembershipChanged()
        {
            Boundary.InvalidateContentObservation();
            Clear();
        }

        internal void DescendantDrawing()
        {
            if (DirectPaintDepth == 0)
            {
                Boundary.InvalidateContentObservation();
            }
        }

        internal bool BeginOwnerDraw()
        {
            bool previousCompletion = CurrentContentCompleted;
            CurrentContentCompleted = false;
            return previousCompletion;
        }

        internal void ContentDrawn() { CurrentContentCompleted = true; }

        internal void EndOwnerDraw(bool previousCompletion, bool completed)
        {
            bool contentCompleted = CurrentContentCompleted;
            CurrentContentCompleted = previousCompletion;
            if (!completed || !contentCompleted)
            {
                Boundary.InvalidateContentObservation();
            }
        }
    }

    public abstract partial class MGElement
    {
        internal MGRenderContentObservation RenderContentBoundary;
        private List<WeakReference<MGRenderContentObservation>> RenderContentObservers;
        internal int RenderContentObserverSlots => RenderContentObservers?.Count ?? 0;

        internal void AddRenderContentObserver(MGRenderContentObservation observer)
        {
            RenderContentObservers ??= new();
            RemoveRenderContentObserver(observer);
            RenderContentObservers.Add(new(observer));
        }

        internal void RemoveRenderContentObserver(MGRenderContentObservation observer)
        {
            if (RenderContentObservers == null)
            {
                return;
            }
            for (int index = RenderContentObservers.Count - 1; index >= 0; index--)
            {
                if (!RenderContentObservers[index].TryGetTarget(out var existing) || ReferenceEquals(existing, observer))
                {
                    RenderContentObservers.RemoveAt(index);
                }
            }
        }

        private void NotifyRenderContentDraw()
        {
            if (RenderContentObservers == null)
            {
                return;
            }
            for (int index = RenderContentObservers.Count - 1; index >= 0; index--)
            {
                if (RenderContentObservers[index].TryGetTarget(out var observer))
                {
                    observer.DescendantDrawing();
                }
                else
                {
                    RenderContentObservers.RemoveAt(index);
                }
            }
        }

        private void NotifyRenderContentMembershipChanged()
        {
            if (RenderContentObservers == null || RenderContentObservers.Count == 0)
            {
                return;
            }

            // Clearing a registration removes it from every publisher, including this one.
            foreach (var reference in RenderContentObservers.ToArray())
            {
                if (reference.TryGetTarget(out var observer))
                {
                    observer.MembershipChanged();
                }
            }
            RemoveRenderContentObserver(null);
        }
    }
}
