using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    /// <summary>A wrapper class that allows you to manipulate the rectangular region that the nested <see cref="IFillBrush"/> is applied to.</summary>
    public partial class MGPaddedFillBrush : IFillBrush
    {
        private IFillBrush _Brush;
        public IFillBrush Brush
        {
            get => _Brush;
            set
            {
                if (!EqualityComparer<IFillBrush>.Default.Equals(_Brush, value))
                {
                    _Brush = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }
        private Thickness _Padding;
        public Thickness Padding
        {
            get => _Padding;
            set
            {
                if (!EqualityComparer<Thickness>.Default.Equals(_Padding, value))
                {
                    _Padding = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }

        private float? _Scale;
        public float? Scale
        {
            get => _Scale;
            set
            {
                if (!EqualityComparer<float?>.Default.Equals(_Scale, value))
                {
                    _Scale = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }

        private int? _MinWidth;
        public int? MinWidth
        {
            get => _MinWidth;
            set
            {
                if (!EqualityComparer<int?>.Default.Equals(_MinWidth, value))
                {
                    _MinWidth = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }
        private int? _MinHeight;
        public int? MinHeight
        {
            get => _MinHeight;
            set
            {
                if (!EqualityComparer<int?>.Default.Equals(_MinHeight, value))
                {
                    _MinHeight = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }
        private int? _MaxWidth;
        public int? MaxWidth
        {
            get => _MaxWidth;
            set
            {
                if (!EqualityComparer<int?>.Default.Equals(_MaxWidth, value))
                {
                    _MaxWidth = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }
        private int? _MaxHeight;
        public int? MaxHeight
        {
            get => _MaxHeight;
            set
            {
                if (!EqualityComparer<int?>.Default.Equals(_MaxHeight, value))
                {
                    _MaxHeight = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }

        private HorizontalAlignment? _HorizontalAlignment;
        public HorizontalAlignment? HorizontalAlignment
        {
            get => _HorizontalAlignment;
            set
            {
                if (!EqualityComparer<HorizontalAlignment?>.Default.Equals(_HorizontalAlignment, value))
                {
                    _HorizontalAlignment = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }
        private VerticalAlignment? _VerticalAlignment;
        public VerticalAlignment? VerticalAlignment
        {
            get => _VerticalAlignment;
            set
            {
                if (!EqualityComparer<VerticalAlignment?>.Default.Equals(_VerticalAlignment, value))
                {
                    _VerticalAlignment = value;
                    _RenderCacheSource?.Invalidate();
                }
            }
        }

        public MGPaddedFillBrush(IFillBrush Brush, Thickness Padding, float? Scale = null, int? MinWidth = null, int? MinHeight = null, int? MaxWidth = null, int? MaxHeight = null,
            HorizontalAlignment? HorizontalAlignment = null, VerticalAlignment? VerticalAlignment = null)
        {
            this.Brush = Brush;
            this.Padding = Padding;

            this.Scale = Scale;

            this.MinWidth = MinWidth;
            this.MinHeight = MinHeight;
            this.MaxWidth = MaxWidth;
            this.MaxHeight = MaxHeight;

            this.HorizontalAlignment = HorizontalAlignment;
            this.VerticalAlignment = VerticalAlignment;
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
        {
            float Opacity = DA.Opacity;
            if (Opacity > 0 && !Opacity.IsAlmostZero() && Brush != null)
            {
                Rectangle PaddedBounds = Bounds.GetCompressed(Padding);

                if (Scale.HasValue)
                    PaddedBounds = PaddedBounds.GetScaledFromCenter(Scale.Value);

                int DesiredWidth = Math.Clamp(PaddedBounds.Width, MinWidth ?? 0, MaxWidth ?? int.MaxValue);
                int DesiredHeight = Math.Clamp(PaddedBounds.Height, MinHeight ?? 0, MaxHeight ?? int.MaxValue);

                //Rectangle ClampedBounds = new(
                //    PaddedBounds.Left - (DesiredWidth - PaddedBounds.Width) / 2, 
                //    PaddedBounds.Top - (DesiredHeight - PaddedBounds.Height) / 2, 
                //    DesiredWidth, DesiredHeight);

                Rectangle ActualBounds = MGElement.ApplyAlignment(PaddedBounds, HorizontalAlignment ?? UI.HorizontalAlignment.Stretch, VerticalAlignment ?? UI.VerticalAlignment.Stretch, new Size(DesiredWidth, DesiredHeight));

                Brush.Draw(DA, Element, ActualBounds);
            }
        }

        public IFillBrush Copy() => new MGPaddedFillBrush(Brush?.Copy(), Padding, Scale, MinWidth, MinHeight, MaxWidth, MaxHeight, HorizontalAlignment, VerticalAlignment);
    }
}
