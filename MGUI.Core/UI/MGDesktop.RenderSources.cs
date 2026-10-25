using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using System;
using System.Linq;

namespace MGUI.Core.UI
{
    public partial class MGDesktop
    {
        private MGRenderCacheSourceDependencies ResourceRenderSources;
        internal void EnableRenderResourceTracking()
        {
            if (ResourceRenderSources == null)
            {
                ResourceRenderSources = new(this, static owner => ((MGDesktop)owner).OnRenderResourcesChanged());
                ResourceRenderSources.ReplaceSources(new object[] { Resources });
            }
        }

        private void InitializeRenderResourceNotifications()
        {
            var weak = new WeakReference<MGDesktop>(this);
            FontManager fonts = FontManager;
            EventHandler fontsChanged = null;
            fontsChanged = (sender, args) =>
            {
                if (weak.TryGetTarget(out var desktop))
                {
                    desktop.OnFontResolverChanged();
                }
                else
                {
                    fonts.FontsChanged -= fontsChanged;
                }
            };
            fonts.FontsChanged += fontsChanged;
            MainRenderer renderer = Renderer;
            EventHandler<EventArgs<ITextEngine>> engineChanged = null;
            engineChanged = (sender, args) =>
            {
                if (weak.TryGetTarget(out var desktop))
                {
                    if (!ReferenceEquals(args.PreviousValue, args.NewValue))
                    {
                        desktop.OnFontResolverChanged();
                    }
                }
                else
                {
                    renderer.TextEngineChanged -= engineChanged;
                }
            };
            renderer.TextEngineChanged += engineChanged;
        }
        private void OnRenderResourcesChanged()
        {
            InvalidateResourcePaintContext(ResourceRenderSources.LastNotificationStamp);
        }
        private void OnFontResolverChanged()
        {
            InvalidateResourcePaintContext(MGRenderRevisionClock.NextMutation());
            RefreshTextLayoutCaches();
        }
        private void InvalidateResourcePaintContext(MGRenderMutationStamp stamp)
        {
            if (ResourceRenderSources == null)
            {
                return;
            }
            ReconcileRenderRootAttachments();
            foreach (MGWindow window in CurrentRenderRoots.ToArray())
            {
                window.InvalidateRenderContextSubtree(MGRenderInvalidationKind.Visual, stamp);
            }
        }
    }
}
