using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI
{
    internal enum MGFillBrushPaintSite
    {
        BackgroundUnderlay,
        BackgroundOverlay,
        Overlay
    }

    public abstract partial class MGElement
    {
        private bool _IsRenderVolatile;

        /// <summary>Declares painting live until cleared. Affects caching only, never normal updates or layout.</summary>
        public bool IsRenderVolatile
        {
            get => _IsRenderVolatile;
            set
            {
                if (_IsRenderVolatile != value)
                {
                    _IsRenderVolatile = value;
                    InvalidateRenderState(MGRenderInvalidationKind.Visual);
                    NPC(nameof(IsRenderVolatile));
                }
            }
        }

        /// <summary>Intrinsic time-driven painting; an explicit false flag cannot suppress it.</summary>
        protected internal virtual bool IsBuiltInRenderVolatile => false;

        private void DrawFillBrushAtSite(IFillBrush brush, ElementDrawArgs args, Rectangle bounds, MGFillBrushPaintSite site)
        {
            if (brush is MGCachedFillBrush cached)
            {
                cached.DrawAtSite(args, this, bounds, site);
            }
            else
            {
                brush?.Draw(args, this, bounds);
            }
        }
    }
}
