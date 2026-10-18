using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    /// <summary>An attachment identity shared by observations in one independently drawn window.
    /// Old identities stay detached permanently, including after the same window is reinserted.</summary>
    internal sealed class MGRenderRootAttachment
    {
        internal bool IsAttached { get; private set; } = true;
        internal void Detach() => IsAttached = false;
    }

    public partial class MGWindow
    {
        internal MGRenderRootAttachment RenderRootAttachment { get; set; }
    }

    public partial class MGDesktop
    {
        private Dictionary<MGWindow, (MGRenderRootAttachment Parent, MGRenderRootAttachment Attachment)> RenderRoots;
        private HashSet<MGWindow> CurrentRenderRoots;
        private List<MGWindow> RemovedRenderRoots;

        /// <summary>Lifecycle hook for cache registrations to retire detached-root pending/ready work.</summary>
        internal event Action<MGWindow, MGRenderRootAttachment> RenderRootDetached;

        /// <summary>Preserves the public Windows List contract. Raw edits become observable at Update,
        /// Prepare or Draw entry, never through an idle scan of the windows' element subtrees.</summary>
        internal void ReconcileRenderRootAttachments()
        {
            RenderRoots ??= new(ReferenceEqualityComparer.Instance);
            CurrentRenderRoots ??= new(ReferenceEqualityComparer.Instance);
            RemovedRenderRoots ??= new();
            CurrentRenderRoots.Clear();
            if (Windows != null)
            {
                foreach (MGWindow Window in Windows)
                {
                    ReconcileRenderRoot(Window, null);
                }
            }

            if (OverlayHost?.ActiveOverlay != null)
            {
                ReconcileRenderRoot(OverlayWindow, null);
            }

            ReconcileRenderRoot(ActiveToolTip, null);
            ReconcileRenderRoot(ActiveContextMenu, null);

            // Removed windows must leave this dictionary, so it never becomes a lifetime root.
            foreach (var Entry in RenderRoots)
            {
                if (!CurrentRenderRoots.Contains(Entry.Key))
                {
                    RemovedRenderRoots.Add(Entry.Key);
                }
            }

            foreach (MGWindow Window in RemovedRenderRoots)
            {
                DetachRenderRoot(Window, RenderRoots[Window].Attachment);
                RenderRoots.Remove(Window);
            }
            RemovedRenderRoots.Clear();
        }

        private void ReconcileRenderRoot(MGWindow Window, MGRenderRootAttachment Parent)
        {
            if (Window == null || !CurrentRenderRoots.Add(Window))
            {
                return;
            }

            if (!RenderRoots.TryGetValue(Window, out var Existing) || !ReferenceEquals(Existing.Parent, Parent))
            {
                if (Existing.Attachment != null)
                {
                    DetachRenderRoot(Window, Existing.Attachment);
                }

                MGRenderRootAttachment Attachment = new();
                RenderRoots[Window] = (Parent, Attachment);
                Window.RenderRootAttachment = Attachment;
                Window.InvalidateRenderState(MGRenderInvalidationKind.Structure);
            }

            if (Window.NestedWindows != null)
            {
                for (int Index = 0; Index < Window.NestedWindows.Count; ++Index)
                {
                    ReconcileRenderRoot(Window.NestedWindows[Index], Window.RenderRootAttachment);
                }
            }

            ReconcileRenderRoot(Window.ModalWindow, Window.RenderRootAttachment);
            if (Window is MGContextMenu Menu)
            {
                ReconcileRenderRoot(Menu.ActiveContextMenu, Window.RenderRootAttachment);
            }
        }

        private void DetachRenderRoot(MGWindow Window, MGRenderRootAttachment Attachment)
        {
            Attachment.Detach();
            if (ReferenceEquals(Window.RenderRootAttachment, Attachment))
            {
                Window.RenderRootAttachment = null;
            }

            Window.InvalidateRenderState(MGRenderInvalidationKind.Structure);
            RenderRootDetached?.Invoke(Window, Attachment);
        }
    }
}
