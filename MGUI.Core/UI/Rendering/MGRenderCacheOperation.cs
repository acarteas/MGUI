using MGUI.Shared.Rendering;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI
{
    /// <summary>
    /// Stable, weakly registered paint-site identity for brush and content producers. Producers maintain their own
    /// source/eligibility token; they must reject unsupported participants, hooks, overflow and unresolved layout.
    /// No tree discovery is performed by this protocol. Do not retain caller transactions in a producer.
    /// </summary>
    internal abstract class MGRenderCacheOperation
    {
        private readonly WeakReference<MGElement> Owner;
        private readonly MGDesktop Desktop;
        private readonly MGRenderCacheRegistration Registration;
        private MGRenderRootAttachment Attachment;
        private MGRenderCacheKey? Observation;
        private Viewport ObservedViewport;
        private Point ObservedBackBuffer;
        private ulong ObservationSequence;
        internal MGRenderCacheOperation(MGDesktop desktop, MGElement owner)
        {
            Desktop = desktop;
            Owner = new(owner);
            Registration = desktop.RenderCaches.Register(owner, this);
            OwnerInvalidationHub.Subscribe(owner, this);
            RootInvalidationHub.Subscribe(desktop, this);
        }

        /// <summary>Must be constant-time, and check maintained source state, support, layout and lifetimes.</summary>
        protected abstract bool IsSourceCurrent(MGElement owner, MGRenderCacheKey key);
        protected abstract void Paint(MGElement owner, ElementDrawArgs args, Rectangle bounds);
        protected virtual void OnRootDetached() { }

        internal void Invalidate()
        {
            if (ObservationSequence == ulong.MaxValue)
            {
                MGRenderRevisionClock.RestartEpoch();
                ObservationSequence = 0;
            }
            ObservationSequence++;
            Observation = null;
            Registration.Invalidate();
        }

        /// <summary>Call at the original paint site. A miss must be followed by BeginDirect/CompleteDirect around paint.</summary>
        internal bool TryDraw(ElementDrawArgs args, Rectangle bounds, ulong sourceRevision, ulong contextRevision = 1)
        {
            if (!Desktop.CanUseRenderCaches || args.DT.IsCacheCapture || !Owner.TryGetTarget(out var owner)
                || !Eligible(args, bounds) || Observation is not { } key || !Current(owner, key)
                || !SameContext(key, owner, args, bounds, sourceRevision, contextRevision))
            {
                return false;
            }
            if (!Desktop.RenderCaches.TryGet(Registration, key, out var target))
            {
                return false;
            }
            args.DT.DrawTextureTo(target, null, bounds.GetTranslated(args.Offset), Color.White);
            return true;
        }

        /// <summary>Always call before direct paint, including ineligible contexts. Capture never changes observations.</summary>
        internal MGRenderCacheKey? BeginDirect(ElementDrawArgs args, Rectangle bounds, ulong sourceRevision,
            bool eligible, ulong contextRevision = 1)
        {
            if (args.DT.IsCacheCapture || Desktop.IsPreparingRenderCaches)
            {
                return null;
            }
            Invalidate();
            if (!eligible || !Owner.TryGetTarget(out var owner) || !Eligible(args, bounds))
            {
                return null;
            }
            Attachment = owner.SelfOrParentWindow.RenderRootAttachment;
            if (Attachment?.IsAttached != true)
            {
                return null;
            }
            var revisions = owner.GetRenderRevisionSnapshot();
            ObservedViewport = args.DT.GD.Viewport;
            ObservedBackBuffer = Desktop.RenderCacheBackBufferSize;
            return new(revisions.Epoch, revisions.Visual, revisions.Layout, revisions.Structure, sourceRevision,
                ObservationSequence, contextRevision, 1, bounds, args.Offset, bounds.Width, bounds.Height,
                (int)args.VisualState.Primary | ((int)args.VisualState.Secondary << 8), args.Opacity, args.DT.CurrentSettings.Transform);
        }

        /// <summary>Call only after successful direct final paint. Exceptions must not acknowledge an observation.</summary>
        internal void CompleteDirect(MGRenderCacheKey? snapshot)
        {
            if (snapshot is { } key && Owner.TryGetTarget(out var owner) && Current(owner, key))
            {
                Observation = key;
                Desktop.RenderCaches.Queue(Registration, key);
            }
        }
        private static bool SameContext(MGRenderCacheKey key, MGElement owner, ElementDrawArgs args, Rectangle bounds,
            ulong sourceRevision, ulong contextRevision)
            => key.Bounds == bounds && key.Offset == args.Offset && key.SourceRevision == sourceRevision
                && key.RenderContext == contextRevision && key.Opacity == args.Opacity && key.Transform == args.DT.CurrentSettings.Transform
                && key.VisualState == ((int)args.VisualState.Primary | ((int)args.VisualState.Secondary << 8));
        internal bool Validate(object owner, MGRenderCacheKey key)
            => Owner.TryGetTarget(out var element) && ReferenceEquals(owner, element) && Observation == key && Current(element, key);
        private static bool SameViewport(Viewport a, Viewport b)
            => a.Bounds == b.Bounds && a.MinDepth == b.MinDepth && a.MaxDepth == b.MaxDepth;
        private bool Current(MGElement owner, MGRenderCacheKey key)
            => owner.IsLayoutValid && key.Observation == ObservationSequence && SameViewport(ObservedViewport, Desktop.RenderCacheViewport)
                && ObservedBackBuffer == Desktop.RenderCacheBackBufferSize && Attachment?.IsAttached == true && ReferenceEquals(Attachment, owner.SelfOrParentWindow.RenderRootAttachment)
                && owner.IsRenderRevisionCurrent(new(key.RevisionEpoch, key.VisualRevision, key.LayoutRevision, key.StructureRevision))
                && IsSourceCurrent(owner, key);
        internal void Capture(object owner, MGRenderCacheKey key, DrawTransaction transaction)
        {
            var state = new VisualState((PrimaryVisualState)(key.VisualState & 255), (SecondaryVisualState)(key.VisualState >> 8));
            Paint((MGElement)owner, new(new(Desktop.Renderer.UpdateArgs.TotalElapsed, transaction, key.Opacity), state, key.Offset), key.Bounds);
        }

        internal static int GetRootListenerCount(MGDesktop desktop) => RootInvalidationHub.Count(desktop);

        // One dispatcher per publisher; change-time registration prunes dead operations without tree scans.
        private sealed class OwnerInvalidationHub
        {
            private static readonly ConditionalWeakTable<MGElement, OwnerInvalidationHub> Hubs = new();
            private readonly List<WeakReference<MGRenderCacheOperation>> Operations = new();
            private OwnerInvalidationHub(MGElement owner) { owner.RenderInvalidated += Changed; }
            internal static void Subscribe(MGElement owner, MGRenderCacheOperation operation)
            {
                var hub = Hubs.GetValue(owner, static publisher => new(publisher));
                Prune(hub.Operations);
                hub.Operations.Add(new(operation));
            }
            private void Changed(MGElement owner, MGRenderInvalidationKind kind, MGRenderMutationStamp stamp)
            {
                for (int index = Operations.Count - 1; index >= 0; index--)
                {
                    if (Operations[index].TryGetTarget(out var operation))
                    {
                        operation.Invalidate();
                    }
                    else
                    {
                        Operations.RemoveAt(index);
                    }
                }
            }
        }
        private sealed class RootInvalidationHub
        {
            private static readonly ConditionalWeakTable<MGDesktop, RootInvalidationHub> Hubs = new();
            private readonly List<WeakReference<MGRenderCacheOperation>> Operations = new();
            private RootInvalidationHub(MGDesktop desktop) { desktop.RenderRootDetached += Detached; }
            internal static void Subscribe(MGDesktop desktop, MGRenderCacheOperation operation)
            {
                var hub = Hubs.GetValue(desktop, static publisher => new(publisher));
                Prune(hub.Operations);
                hub.Operations.Add(new(operation));
            }
            internal static int Count(MGDesktop desktop) => Hubs.TryGetValue(desktop, out var hub) ? hub.Operations.Count : 0;
            private void Detached(MGWindow window, MGRenderRootAttachment attachment)
            {
                for (int index = Operations.Count - 1; index >= 0; index--)
                {
                    if (!Operations[index].TryGetTarget(out var operation))
                    {
                        Operations.RemoveAt(index);
                    }
                    else if (ReferenceEquals(operation.Attachment, attachment))
                    {
                        operation.Invalidate();
                        operation.OnRootDetached();
                    }
                }
            }
        }
        private static void Prune(List<WeakReference<MGRenderCacheOperation>> operations)
        {
            for (int index = operations.Count - 1; index >= 0; index--)
            {
                if (!operations[index].TryGetTarget(out _))
                {
                    operations.RemoveAt(index);
                }
            }
        }

        private static bool Eligible(ElementDrawArgs args, Rectangle bounds)
            => GetContextBypassReason(args, bounds) == MGRenderCacheBypassReason.None;

        internal static MGRenderCacheBypassReason GetContextBypassReason(ElementDrawArgs args, Rectangle bounds)
        {
            var settings = args.DT.CurrentSettings;
            Matrix m = settings.Transform;
            bool translation = m.M11 == 1 && m.M22 == 1 && m.M33 == 1 && m.M44 == 1
                && m.M12 == 0 && m.M13 == 0 && m.M14 == 0 && m.M21 == 0 && m.M23 == 0 && m.M24 == 0
                && m.M31 == 0 && m.M32 == 0 && m.M34 == 0 && m.M43 == 0
                && float.IsFinite(m.M41) && float.IsFinite(m.M42)
                && m.M41 == MathF.Truncate(m.M41) && m.M42 == MathF.Truncate(m.M42);
            if (!translation) { return MGRenderCacheBypassReason.Transform; }
            if (args.Opacity != 1) { return MGRenderCacheBypassReason.Opacity; }
            if (bounds.Width <= 0 || bounds.Height <= 0) { return MGRenderCacheBypassReason.InvalidBounds; }

            // Double arithmetic avoids wrapping an otherwise integer-valued translation or rectangle edge.
            double left = bounds.X + (double)args.Offset.X + m.M41;
            double top = bounds.Y + (double)args.Offset.Y + m.M42;
            if (left < int.MinValue || top < int.MinValue || left + bounds.Width > int.MaxValue || top + bounds.Height > int.MaxValue)
            {
                return MGRenderCacheBypassReason.InvalidBounds;
            }
            Rectangle screen = new((int)left, (int)top, bounds.Width, bounds.Height);
            var presentation = args.DT.GD.PresentationParameters;
            if (args.DT.GD.Viewport.Bounds != new Rectangle(0, 0, presentation.BackBufferWidth, presentation.BackBufferHeight))
            {
                return MGRenderCacheBypassReason.Viewport;
            }
            if (args.HasUnsupportedInheritedRasterizer || settings.RasterizerType != RasterizerType.SolidScissorTest) { return MGRenderCacheBypassReason.Rasterizer; }
            if (settings.Sort != SpriteSortMode.Deferred) { return MGRenderCacheBypassReason.SortMode; }
            if (settings.BlendType != BlendType.AlphaBlend && settings.BlendType != BlendType.Default) { return MGRenderCacheBypassReason.Blend; }
            if (settings.SamplerType != SamplerType.PointClamp) { return MGRenderCacheBypassReason.Sampler; }
            if (settings.DepthStencilType != DepthStencilType.None && settings.DepthStencilType != DepthStencilType.Default) { return MGRenderCacheBypassReason.Depth; }
            if (settings.Effect != null) { return MGRenderCacheBypassReason.Effect; }
            if (!args.DT.GD.ScissorRectangle.Contains(screen)) { return MGRenderCacheBypassReason.PartialClip; }
            return MGRenderCacheBypassReason.None;
        }
    }
}
