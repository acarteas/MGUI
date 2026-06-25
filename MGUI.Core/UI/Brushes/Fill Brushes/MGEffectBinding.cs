using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    internal sealed class MGEffectBinding
    {
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

        public Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect { get; set; }
        public bool UseStandardParameters { get; set; }

        private MGEffectParameterValue[] _Parameters = Array.Empty<MGEffectParameterValue>();
        public IReadOnlyList<MGEffectParameterValue> Parameters
        {
            get => _Parameters;
            set => _Parameters = value?.ToArray() ?? Array.Empty<MGEffectParameterValue>();
        }

        private readonly Dictionary<string, EffectParameter> ParameterCache = new();

        public MGEffectBinding(
            Effect Effect,
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect = null)
        {
            this.Effect = Effect;
            this.ConfigureEffect = ConfigureEffect;
        }

        public void Apply(
            MGStandardEffectParameterValues? StandardValues,
            ElementDrawArgs DA,
            MGElement Element,
            Rectangle Bounds)
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

        public MGEffectBinding Copy()
        {
            return new MGEffectBinding(Effect, ConfigureEffect)
            {
                UseStandardParameters = UseStandardParameters,
                Parameters = _Parameters
            };
        }

        public void ApplyElementTextureCoordinateMapping(MGElementTextureCoordinateMapping Mapping)
        {
            if (Effect == null || !UseStandardParameters)
            {
                return;
            }

            SetParameter("ElementTextureCoordinateScale", nameof(Vector2), x => x.SetValue(Mapping.Scale));
            SetParameter("ElementTextureCoordinateOffset", nameof(Vector2), x => x.SetValue(Mapping.Offset));
        }

        public static MGStandardEffectParameterValues CalculateStandardParameters(
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

        private EffectParameter GetParameter(string Name)
        {
            if (!ParameterCache.TryGetValue(Name, out EffectParameter Parameter))
            {
                Parameter = Effect.Parameters[Name];
                ParameterCache.Add(Name, Parameter);
            }

            return Parameter;
        }
    }
}
