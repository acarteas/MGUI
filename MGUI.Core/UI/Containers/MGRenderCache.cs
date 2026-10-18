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
        private bool IsContentEligible => GetContentBypassReason() == MGRenderCacheBypassReason.None;

        /// <summary>The source/context decision at the most recent ordinary DrawContents invocation.
        /// A skipped paint site leaves this value unchanged; None does not promise a ready texture.
        /// Reading this diagnostic never discovers descendants or changes rendering.</summary>
        public MGRenderCacheBypassReason LastBypassReason { get; private set; }

        private MGRenderCacheBypassReason GetContentBypassReason()
        {
            if (GetType() != typeof(MGRenderCache)) { return MGRenderCacheBypassReason.UnsupportedRenderer; }
            if (!HasCacheableContent) { return MGRenderCacheBypassReason.DisabledOrEmpty; }
            if (!ClipToBounds) { return MGRenderCacheBypassReason.UnboundedPaint; }
            if (!IsLayoutValid) { return MGRenderCacheBypassReason.UnresolvedLayout; }
            if (IsRenderVolatile || IsBuiltInRenderVolatile) { return MGRenderCacheBypassReason.Volatile; }
            var facts = Content.GetRenderEligibilitySnapshot();
            if (facts.PublicDrawCallbackCount != 0) { return MGRenderCacheBypassReason.PublicDrawCallback; }
            if (facts.VolatileCount != 0) { return MGRenderCacheBypassReason.Volatile; }
            if (facts.UnsupportedTransformCount != 0) { return MGRenderCacheBypassReason.Transform; }
            if (facts.UnboundedPaintCount != 0) { return MGRenderCacheBypassReason.UnboundedPaint; }
            if (facts.UnresolvedLayoutCount != 0) { return MGRenderCacheBypassReason.UnresolvedLayout; }
            if (facts.UnsupportedCount != 0) { return MGRenderCacheBypassReason.UnsupportedRenderer; }
            return MGRenderCacheBypassReason.None;
        }

        internal void InvalidateContentObservation() => Operation?.Invalidate();

        protected override void DrawContents(ElementDrawArgs DA)
        {
            if (DA.DT.IsCacheCapture || SelfOrParentWindow.Desktop.IsPreparingRenderCaches)
            {
                base.DrawContents(DA);
                return;
            }

            LastBypassReason = GetContentBypassReason();
            bool eligible = LastBypassReason == MGRenderCacheBypassReason.None;
            if (eligible)
            {
                LastBypassReason = MGRenderCacheOperation.GetContextBypassReason(DA, LayoutBounds);
            }
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
