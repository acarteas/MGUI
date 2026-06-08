using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>
        /// The runtime MonoGame effect to apply while drawing this fill. This brush does not dispose or clone the effect.
        /// </summary>
        private Effect _Effect;
        public Effect Effect
        {
            get => _Effect;
            set
            {
                if (_Effect != value)
                {
                    _Effect = value;
                    ParameterCache.Clear();
                }
            }
        }

        /// <summary>
        /// Optional callback invoked immediately before drawing when <see cref="Effect"/> is not null.
        /// Use this to set effect parameters from the draw args, target element, and target bounds.
        /// </summary>
        public Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect { get; set; }

        /// <summary>Whether MGUI's conventional draw, bounds, time, and visual-state parameters are set when present.</summary>
        public bool UseStandardParameters { get; set; }

        private MGEffectParameterValue[] _Parameters = Array.Empty<MGEffectParameterValue>();

        /// <summary>Application-specific constant parameters applied before <see cref="ConfigureEffect"/>.</summary>
        public IReadOnlyList<MGEffectParameterValue> Parameters
        {
            get => _Parameters;
            set => _Parameters = value?.ToArray() ?? Array.Empty<MGEffectParameterValue>();
        }

        private readonly Dictionary<string, EffectParameter> ParameterCache = new();

        public MGEffectFillBrush(Effect Effect)
            : this(Effect, null)
        {
        }

        public MGEffectFillBrush(Effect Effect, Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect)
        {
            this.Effect = Effect;
            this.ConfigureEffect = ConfigureEffect;
            UseStandardParameters = false;
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
                ApplyEffectConfiguration(StandardValues, DA, Element, Bounds);
            }

            using (DA.DT.SetEffectTemporary(Effect))
            {
                DA.DT.DrawTextureTo(DA.DT.WhitePixel, null, Bounds.GetTranslated(DA.Offset), ColorMask);
            }
        }

        private EffectParameter GetParameter(string Name)
        {
            if (!ParameterCache.TryGetValue(Name, out EffectParameter Parameter))
            {
                Parameter = Effect.Parameters[Name];
                ParameterCache.Add(Name, Parameter);
            }

            return Parameter;
        }

        internal void ApplyEffectConfiguration(MGStandardEffectParameterValues? StandardValues, ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
        {
            if (Effect == null)
            {
                return;
            }

            if (UseStandardParameters && StandardValues.HasValue)
            {
                ApplyStandardParameters(StandardValues.Value);
            }

            ApplyCustomParameters();
            ConfigureEffect?.Invoke(Effect, DA, Element, Bounds);
        }

        private void ApplyStandardParameters(MGStandardEffectParameterValues Values)
        {
            SetParameter("MatrixTransform", nameof(Matrix), x => x.SetValue(Values.MatrixTransform));
            SetParameter("ElementPosition", nameof(Vector2), x => x.SetValue(Values.ElementPosition));
            SetParameter("ElementSize", nameof(Vector2), x => x.SetValue(Values.ElementSize));
            SetParameter("Opacity", nameof(Single), x => x.SetValue(Values.Opacity));
            SetParameter("TimeSeconds", nameof(Single), x => x.SetValue(Values.TimeSeconds));
            SetParameter("HoverAmount", nameof(Single), x => x.SetValue(Values.HoverAmount));
            SetParameter("PressAmount", nameof(Single), x => x.SetValue(Values.PressAmount));
            SetParameter("SelectedAmount", nameof(Single), x => x.SetValue(Values.SelectedAmount));
            SetParameter("DisabledAmount", nameof(Single), x => x.SetValue(Values.DisabledAmount));
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
        {
            Matrix Projection = Matrix.CreateOrthographicOffCenter(0, Viewport.Width, Viewport.Height, 0, 0, -1);
            if (UseHalfPixelOffset)
            {
                Projection.M41 += -0.5f * Projection.M11;
                Projection.M42 += -0.5f * Projection.M22;
            }
            Rectangle TranslatedBounds = Bounds.GetTranslated(Offset);
            return new MGStandardEffectParameterValues(
                Transform * Projection,
                new Vector2(TranslatedBounds.X, TranslatedBounds.Y),
                new Vector2(Math.Max(1, Bounds.Width), Math.Max(1, Bounds.Height)),
                Opacity,
                (float)Time.TotalSeconds,
                VisualState.IsHovered ? 1.0f : 0.0f,
                VisualState.IsPressed ? 1.0f : 0.0f,
                VisualState.IsSelected ? 1.0f : 0.0f,
                VisualState.IsDisabled ? 1.0f : 0.0f);
        }

        private void ApplyCustomParameters()
        {
            foreach (MGEffectParameterValue Value in Parameters)
            {
                switch (Value.Type)
                {
                    case MGEffectParameterType.Float:
                        SetParameter(Value.Name, nameof(Single), x => x.SetValue((float)Value.Value));
                        break;
                    case MGEffectParameterType.Int:
                        SetParameter(Value.Name, nameof(Int32), x => x.SetValue((int)Value.Value));
                        break;
                    case MGEffectParameterType.Bool:
                        SetParameter(Value.Name, nameof(Boolean), x => x.SetValue((bool)Value.Value));
                        break;
                    case MGEffectParameterType.Color:
                        SetParameter(Value.Name, "Color/Vector4", x => x.SetValue((Vector4)Value.Value));
                        break;
                    case MGEffectParameterType.Vector2:
                        SetParameter(Value.Name, nameof(Vector2), x => x.SetValue((Vector2)Value.Value));
                        break;
                    case MGEffectParameterType.Vector3:
                        SetParameter(Value.Name, nameof(Vector3), x => x.SetValue((Vector3)Value.Value));
                        break;
                    case MGEffectParameterType.Vector4:
                        SetParameter(Value.Name, nameof(Vector4), x => x.SetValue((Vector4)Value.Value));
                        break;
                    default:
                        throw new NotImplementedException($"Unrecognized {nameof(MGEffectParameterType)}: {Value.Type}");
                }
            }
        }

        private void SetParameter(string Name, string RuntimeType, Action<EffectParameter> SetValue)
        {
            EffectParameter Parameter = GetParameter(Name);
            if (Parameter == null)
            {
                return;
            }

            try
            {
                SetValue(Parameter);
            }
            catch (Exception Ex)
            {
                throw new InvalidOperationException(
                    $"Effect parameter '{Name}' cannot accept the configured {RuntimeType} value. " +
                    $"The shader declares class {Parameter.ParameterClass} and type {Parameter.ParameterType}.", Ex);
            }
        }

        public IFillBrush Copy() => new MGEffectFillBrush(Effect, ConfigureEffect)
        {
            UseStandardParameters = UseStandardParameters,
            Parameters = _Parameters
        };
    }
}
