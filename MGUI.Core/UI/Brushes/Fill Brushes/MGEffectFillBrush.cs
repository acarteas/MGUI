using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public enum MGEffectParameterType
    {
        Float,
        Int,
        Bool,
        Color,
        Vector2,
        Vector3,
        Vector4
    }

    /// <summary>A constant value applied to an optional shader parameter immediately before an effect fill is drawn.</summary>
    public readonly record struct MGEffectParameterValue(string Name, MGEffectParameterType Type, object Value);

    internal readonly record struct MGStandardEffectParameterValues(
        Matrix MatrixTransform,
        Vector2 ElementPosition,
        Vector2 ElementSize,
        float Opacity,
        float TimeSeconds,
        float HoverAmount,
        float PressAmount,
        float SelectedAmount,
        float DisabledAmount);

    /// <summary>
    /// An <see cref="IFillBrush"/> that fills its bounds by drawing a white pixel through a caller-supplied MonoGame <see cref="Effect"/>.
    /// The consuming game owns shader compilation, backend compatibility, parameter naming, and the lifetime of <see cref="Effect"/>.
    /// When <see cref="Effect"/> is null, this brush draws the same white fill without applying an effect.
    /// </summary>
    public class MGEffectFillBrush : IFillBrush
    {
        private readonly MGEffectBinding Binding;

        /// <summary>
        /// The runtime MonoGame effect to apply while drawing this fill. This brush does not dispose or clone the effect.
        /// </summary>
        public Effect Effect
        {
            get => Binding.Effect;
            set => Binding.Effect = value;
        }

        /// <summary>
        /// Optional callback invoked immediately before drawing when <see cref="Effect"/> is not null.
        /// Use this to set effect parameters from the draw args, target element, and target bounds.
        /// </summary>
        public Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect
        {
            get => Binding.ConfigureEffect;
            set => Binding.ConfigureEffect = value;
        }

        /// <summary>Whether MGUI's conventional draw, bounds, time, and visual-state parameters are set when present.</summary>
        public bool UseStandardParameters
        {
            get => Binding.UseStandardParameters;
            set => Binding.UseStandardParameters = value;
        }

        /// <summary>Application-specific constant parameters applied before <see cref="ConfigureEffect"/>.</summary>
        public IReadOnlyList<MGEffectParameterValue> Parameters
        {
            get => Binding.Parameters;
            set => Binding.Parameters = value;
        }

        public MGEffectFillBrush(Effect Effect)
            : this(Effect, null)
        {
        }

        public MGEffectFillBrush(Effect Effect, Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect)
        {
            Binding = new MGEffectBinding(Effect, ConfigureEffect);
        }

        private MGEffectFillBrush(MGEffectBinding Binding)
        {
            this.Binding = Binding;
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
                MGStandardEffectParameterValues? StandardValues = UseStandardParameters
                    ? CalculateStandardParameters(DA.TS, DA.DT.CurrentSettings.Transform, DA.DT.GD.Viewport,
                        DA.DT.GD.UseHalfPixelOffset, DA.VisualState, DA.Offset, DA.Opacity, Bounds)
                    : null;
                Binding.Apply(StandardValues, DA, Element, Bounds);
            }

            using (DA.DT.SetEffectTemporary(Effect))
            {
                DA.DT.DrawTextureTo(DA.DT.WhitePixel, null, Bounds.GetTranslated(DA.Offset), ColorMask);
            }
        }

        internal void ApplyEffectConfiguration(
            MGStandardEffectParameterValues? StandardValues,
            ElementDrawArgs DA,
            MGElement Element,
            Rectangle Bounds)
        {
            Binding.Apply(StandardValues, DA, Element, Bounds);
        }

        internal static MGStandardEffectParameterValues CalculateStandardParameters(
            TimeSpan Time,
            Matrix Transform,
            Viewport Viewport,
            bool UseHalfPixelOffset,
            VisualState VisualState,
            Point Offset,
            float Opacity,
            Rectangle Bounds)
            => MGEffectBinding.CalculateStandardParameters(
                Time, Transform, Viewport, UseHalfPixelOffset, VisualState, Offset, Opacity, Bounds);

        public IFillBrush Copy() => new MGEffectFillBrush(Binding.Copy());
    }
}
