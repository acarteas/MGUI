using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Containers
{
    /// <summary>Explicit pixel reuse of Content only. Update, layout, input and this wrapper's decorations remain live.
    /// Call MGDesktop.PrepareRenderCaches after Update and before drawing the scene to permit reuse.</summary>
    public class MGRenderCache : MGSingleContentHost
    {
        private bool _IsCacheEnabled = true;
        private ContentOperation Operation;

        /// <summary>Enabled by default on this explicit wrapper. Disabling retires its cached pixels immediately.</summary>
        public bool IsCacheEnabled
        {
            get => _IsCacheEnabled;
            set
            {
                if (_IsCacheEnabled != value)
                {
                    _IsCacheEnabled = value;
                    InvalidateContentObservation();
                    RenderContentBoundary.Clear();
                    InvalidateRenderState(MGRenderInvalidationKind.Visual);
                    NPC(nameof(IsCacheEnabled));
                }
            }
        }

        public MGRenderCache(MGWindow Window) : base(Window, MGElementType.RenderCache)
        {
            RenderContentBoundary = new(this);
            RenderInvalidated += (_, kind, _) =>
            {
                if ((kind & MGRenderInvalidationKind.Structure) != 0 || !HasCacheableContent)
                {
                    RenderContentBoundary.Clear();
                }
            };
        }

        private bool HasCacheableContent => IsCacheEnabled && Content != null && Visibility == Visibility.Visible
            && Content.Visibility == Visibility.Visible && LayoutBounds.Width > 0 && LayoutBounds.Height > 0
            && Content.LayoutBounds.Width > 0 && Content.LayoutBounds.Height > 0;

        // Derived wrappers can change virtual child enumeration or content painting. Their ordinary traversal
        // remains live; participation in an enclosing boundary is governed by the custom support contract.
        private bool IsContentEligible => GetType() == typeof(MGRenderCache) && HasCacheableContent && ClipToBounds && IsLayoutValid
            && !IsRenderVolatile && !IsBuiltInRenderVolatile && Content.GetRenderEligibilitySnapshot().IsEligible;

        internal void InvalidateContentObservation() => Operation?.Invalidate();

        protected override void DrawContents(ElementDrawArgs DA)
        {
            if (DA.DT.IsCacheCapture || SelfOrParentWindow.Desktop.IsPreparingRenderCaches)
            {
                base.DrawContents(DA);
                return;
            }

            bool eligible = IsContentEligible;
            if (!eligible)
            {
                InvalidateContentObservation();
                RenderContentBoundary.Clear();
                base.DrawContents(DA);
                RenderContentBoundary.ContentDrawn();
                return;
            }

            Operation ??= new(this);
            ulong sourceRevision = Content.VisualRevision;
            if (Operation.TryDraw(DA, LayoutBounds, sourceRevision))
            {
                RenderContentBoundary.ContentDrawn();
                return;
            }

            var observation = Operation.BeginDirect(DA, LayoutBounds, sourceRevision, eligible);
            if (observation != null)
            {
                RenderContentBoundary.EnsureRegistered();
            }
            RenderContentBoundary.DirectPaintDepth++;
            try
            {
                base.DrawContents(DA);
                Operation.CompleteDirect(observation);
                RenderContentBoundary.ContentDrawn();
            }
            finally
            {
                RenderContentBoundary.DirectPaintDepth--;
            }
        }

        private sealed class ContentOperation : MGRenderCacheOperation
        {
            private readonly System.WeakReference<MGRenderCache> Boundary;
            internal ContentOperation(MGRenderCache boundary) : base(boundary.SelfOrParentWindow.Desktop, boundary)
            {
                Boundary = new(boundary);
            }
            protected override bool IsSourceCurrent(MGElement owner, MGRenderCacheKey key)
                => owner is MGRenderCache boundary && boundary.IsContentEligible && boundary.Content.VisualRevision == key.SourceRevision;
            protected override void Paint(MGElement owner, ElementDrawArgs args, Rectangle bounds)
                => ((MGRenderCache)owner).Content.Draw(args);
            protected override void OnRootDetached()
            {
                if (Boundary.TryGetTarget(out var boundary))
                {
                    boundary.RenderContentBoundary.Clear();
                }
            }
        }
    }
}
