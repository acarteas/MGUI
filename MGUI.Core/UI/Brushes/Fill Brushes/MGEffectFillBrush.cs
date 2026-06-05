using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;
using System;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    /// <summary>
    /// An <see cref="IFillBrush"/> that fills its bounds by drawing a white pixel through a caller-supplied MonoGame <see cref="Effect"/>.
    /// The consuming game owns shader compilation, backend compatibility, parameter naming, and the lifetime of <see cref="Effect"/>.
    /// When <see cref="Effect"/> is null, this brush draws the same white fill without applying an effect.
    /// </summary>
    public class MGEffectFillBrush : IFillBrush
    {
        /// <summary>
        /// The runtime MonoGame effect to apply while drawing this fill. This brush does not dispose or clone the effect.
        /// </summary>
        public Effect Effect { get; set; }

        /// <summary>
        /// Optional callback invoked immediately before drawing when <see cref="Effect"/> is not null.
        /// Use this to set effect parameters from the draw args, target element, and target bounds.
        /// </summary>
        public Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect { get; set; }

        public MGEffectFillBrush(Effect Effect)
            : this(Effect, null)
        {
        }

        public MGEffectFillBrush(Effect Effect, Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect)
        {
            this.Effect = Effect;
            this.ConfigureEffect = ConfigureEffect;
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
        {
            if (Bounds.Width < 1 || Bounds.Height < 1)
            {
                return;
            }

            Color ColorMask = Color.White * DA.Opacity;
            if (ColorMask == Color.Transparent)
            {
                return;
            }

            if (Effect != null)
            {
                ConfigureEffect?.Invoke(Effect, DA, Element, Bounds);
            }

            using (DA.DT.SetEffectTemporary(Effect))
            {
                DA.DT.DrawTextureTo(DA.DT.WhitePixel, null, Bounds.GetTranslated(DA.Offset), ColorMask);
            }
        }

        public IFillBrush Copy() => new MGEffectFillBrush(Effect, ConfigureEffect);
    }
}
