using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    /// <summary>Maintained contributing-paint facts. Counts include paint edges, not logical popup ownership.
    /// Geometry is the root's clip bound; callers still validate the observed draw context and inherited clip.</summary>
    internal readonly record struct MGRenderEligibilitySnapshot(int UnsupportedCount, int VolatileCount,
        int PublicDrawCallbackCount, int UnsupportedTransformCount, int UnboundedPaintCount,
        int UnresolvedLayoutCount, Rectangle PaintBounds)
    {
        internal bool IsEligible => UnsupportedCount == 0 && VolatileCount == 0 && PublicDrawCallbackCount == 0
            && UnsupportedTransformCount == 0 && UnboundedPaintCount == 0 && UnresolvedLayoutCount == 0;
    }

    public abstract partial class MGElement
    {
        private static readonly HashSet<Type> SupportedRenderCacheTypes = new()
        {
            typeof(MGBorder), typeof(MGButton), typeof(MGChatBox), typeof(MGChatBoxMessage), typeof(MGCheckBox),
            typeof(MGComboBox<>), typeof(MGContentPresenter), typeof(MGContextMenuButton), typeof(MGContextMenuToggle),
            typeof(MGContextMenuRadioButton), typeof(MGContextMenuSeparator), typeof(MGContextualContentPresenter),
            typeof(MGDockPanel), typeof(MGExpander), typeof(MGGrid), typeof(MGGridColorPicker), typeof(MGGridSplitter),
            typeof(MGGroupBox), typeof(MGHeaderedContentPresenter), typeof(MGImage), typeof(MGInputConsumer),
            typeof(MGListBox<>), typeof(MGListView<>), typeof(MGMenuBar), typeof(MGMenuBarItem), typeof(MGOverlay),
            typeof(MGOverlayPanel), typeof(MGPasswordBox), typeof(MGProgressBar), typeof(MGProgressButton),
            typeof(MGRadioButton), typeof(MGRatingControl), typeof(MGRectangle), typeof(MGResizeGrip),
            typeof(MGScrollViewer), typeof(MGSeparator), typeof(MGSlider), typeof(MGSpacer), typeof(MGSpoiler),
            typeof(MGStackPanel), typeof(MGStopwatch), typeof(MGTabControl), typeof(MGTabItem), typeof(MGTextBlock),
            typeof(MGTextBox), typeof(MGTimer), typeof(MGToggleButton), typeof(MGTreeView), typeof(MGUniformGrid)
        };

        /// <summary>Opts custom painting into deterministic, bounded, translation-safe capture.
        /// Unknown subclasses default false, including subclasses of supported built-ins. An override returning true
        /// promises no draw side effects or opaque external-state changes without InvalidateVisual(). Support changes
        /// also require InvalidateVisual(); declared and intrinsic volatility always take precedence.</summary>
        protected virtual bool SupportsRenderCaching => IsBuiltInRenderCacheType(GetType());

        internal static bool IsBuiltInRenderCacheType(Type runtimeType)
            => SupportedRenderCacheTypes.Contains(runtimeType.IsGenericType ? runtimeType.GetGenericTypeDefinition() : runtimeType);

        private sealed class RenderEligibilityState
        {
            internal MGRenderEligibilitySnapshot Local;
            internal MGRenderEligibilitySnapshot Aggregate;
            internal readonly Dictionary<MGElement, MGRenderEligibilitySnapshot> Children = new(ReferenceEqualityComparer.Instance);
            internal bool Initializing;
        }

        private RenderEligibilityState RenderEligibility;
        private bool RenderEligibilityLayoutPending;

        /// <summary>Registers sources and eligibility once. Subsequent reads neither discover nor poll anything.</summary>
        internal MGRenderEligibilitySnapshot GetRenderEligibilitySnapshot()
        {
            EnableRenderEligibilityTracking();
            return RenderEligibility.Aggregate with { PaintBounds = LayoutBounds };
        }

        private void EnableRenderEligibilityTracking()
        {
            if (RenderEligibility != null)
            {
                return;
            }

            RenderEligibility = new() { Initializing = true };
            EnableRenderSourceTracking();
            RenderEligibility.Local = ComputeLocalRenderEligibility();
            RenderEligibility.Aggregate = RenderEligibility.Local;
            foreach (MGElement Child in RenderPaintChildren)
            {
                Child.EnableRenderEligibilityTracking();
                MGRenderEligibilitySnapshot Facts = Child.RenderEligibility.Aggregate;
                RenderEligibility.Children.Add(Child, Facts);
                RenderEligibility.Aggregate = AddRenderEligibility(RenderEligibility.Aggregate, Facts, 1);
            }
            RenderEligibility.Initializing = false;
        }

        private MGRenderEligibilitySnapshot ComputeLocalRenderEligibility()
        {
            bool Unsupported = !SupportsRenderCaching || HasUnsupportedControlRenderState
                || (this is MGTextBlock TextBlock && TextBlock.HasInteractiveTextRuns)
                || BaseRenderSources?.SupportsRenderCaching == false || BaseRenderSources?.ResourcesAreAlive == false;
            bool Volatile = IsRenderVolatile || IsBuiltInRenderVolatile || BaseRenderSources?.IsRenderVolatile == true;
            bool UnsupportedTransform = RenderScale?.TryGetScale(VisualState, out float Scale) == true
                && (!float.IsFinite(Scale) || Scale != 1);
            bool Unbounded = !ClipToBounds || BaseRenderSources?.HasUnboundedPaint == true;
            return new(Unsupported ? 1 : 0, Volatile ? 1 : 0, PublicDrawCallbackCount + ControlPublicDrawCallbackCount,
                UnsupportedTransform ? 1 : 0, Unbounded ? 1 : 0, !IsLayoutValid || RenderEligibilityLayoutPending || IsUpdatingLayout ? 1 : 0, default);
        }

        private static MGRenderEligibilitySnapshot AddRenderEligibility(MGRenderEligibilitySnapshot left, MGRenderEligibilitySnapshot right, int sign)
            => new(left.UnsupportedCount + sign * right.UnsupportedCount, left.VolatileCount + sign * right.VolatileCount,
                left.PublicDrawCallbackCount + sign * right.PublicDrawCallbackCount,
                left.UnsupportedTransformCount + sign * right.UnsupportedTransformCount,
                left.UnboundedPaintCount + sign * right.UnboundedPaintCount,
                left.UnresolvedLayoutCount + sign * right.UnresolvedLayoutCount, default);

        private void RefreshLocalRenderEligibility()
        {
            if (RenderEligibility == null || RenderEligibility.Initializing)
            {
                return;
            }

            MGRenderEligibilitySnapshot Local = ComputeLocalRenderEligibility();
            if (Local != RenderEligibility.Local)
            {
                RenderEligibility.Aggregate = AddRenderEligibility(AddRenderEligibility(RenderEligibility.Aggregate,
                    RenderEligibility.Local, -1), Local, 1);
                RenderEligibility.Local = Local;
                PropagateRenderEligibility(new(ReferenceEqualityComparer.Instance));
            }
        }

        private void ReconcileRenderEligibilityChildren(IReadOnlyList<MGElement> Previous, IReadOnlyList<MGElement> Current)
        {
            if (RenderEligibility == null || RenderEligibility.Initializing)
            {
                return;
            }

            HashSet<MGElement> Membership = new(Current, ReferenceEqualityComparer.Instance);
            foreach (MGElement Child in Previous)
            {
                if (!Membership.Contains(Child) && RenderEligibility.Children.Remove(Child, out MGRenderEligibilitySnapshot Facts))
                {
                    RenderEligibility.Aggregate = AddRenderEligibility(RenderEligibility.Aggregate, Facts, -1);
                }
            }
            foreach (MGElement Child in Current)
            {
                if (!RenderEligibility.Children.ContainsKey(Child))
                {
                    Child.EnableRenderEligibilityTracking();
                    MGRenderEligibilitySnapshot Facts = Child.RenderEligibility.Aggregate;
                    RenderEligibility.Children.Add(Child, Facts);
                    RenderEligibility.Aggregate = AddRenderEligibility(RenderEligibility.Aggregate, Facts, 1);
                }
            }
            PropagateRenderEligibility(new(ReferenceEqualityComparer.Instance));
        }

        private void PropagateRenderEligibility(HashSet<MGElement> Path)
        {
            if (!Path.Add(this) || RenderPaintParents == null)
            {
                return;
            }

            foreach (MGElement Owner in RenderPaintParents)
            {
                RenderEligibilityState State = Owner.RenderEligibility;
                if (State != null && !State.Initializing && State.Children.TryGetValue(this, out MGRenderEligibilitySnapshot Previous)
                    && Previous != RenderEligibility.Aggregate)
                {
                    State.Aggregate = AddRenderEligibility(AddRenderEligibility(State.Aggregate, Previous, -1), RenderEligibility.Aggregate, 1);
                    State.Children[this] = RenderEligibility.Aggregate;
                    Owner.PropagateRenderEligibility(Path);
                }
            }
            Path.Remove(this);
        }
    }
}
