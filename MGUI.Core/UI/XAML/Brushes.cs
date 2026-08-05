using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNAColor = Microsoft.Xna.Framework.Color;
using MGUI.Core.UI.Data_Binding;
using System.Diagnostics;
using Microsoft.Xna.Framework;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    #region Color
    [TypeConverter(typeof(ColorStringConverter))]
    public struct XAMLColor
    {
        internal string StaticResourceKey { get; private set; }

        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public XAMLColor() : this(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) { }
        public XAMLColor(XNAColor Color) : this(Color.R, Color.G, Color.B, Color.A) { }
        public XAMLColor(byte R, byte G, byte B) : this(R, G, B, byte.MaxValue) { }
        public XAMLColor(byte R, byte G, byte B, byte A)
        {
            StaticResourceKey = null;
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = A;
        }

        internal static XAMLColor FromStaticResource(string key) => new() { StaticResourceKey = key };

        public static XAMLColor operator *(XAMLColor value, float scale) =>
            new((byte)(value.R * scale), (byte)(value.G * scale), (byte)(value.B * scale), (byte)(value.A * scale));
        public static XAMLColor operator *(float scale, XAMLColor value) =>
            new((byte)(value.R * scale), (byte)(value.G * scale), (byte)(value.B * scale), (byte)(value.A * scale));

        public override string ToString() => $"({R},{G},{B}|{A})";

        public XNAColor ToXNAColor() => new(R, G, B, A);
    }

    public class ColorStringConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string stringValue ? ParseColor(stringValue) : base.ConvertFrom(context, culture, value);
        public static XAMLColor ParseColor(string Value)
        {
            if (StaticResourceExpression.TryParse(Value, out StaticResourceExpression expression))
            {
                return XAMLColor.FromStaticResource(expression.Key);
            }

            return new(XNAColorStringConverter.ParseColor(Value));
        }
    }
    #endregion Color

    #region Fill Brush
    [TypeConverter(typeof(FillBrushStringConverter))]
    public abstract class FillBrush : XAMLBindableBase
    {
        public abstract IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element);
    }

    public class FillBrushStringConverter : TypeConverter
    {
        private readonly ColorStringConverter ColorStringConverter = new();
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => ColorStringConverter.CanConvertFrom(context, sourceType) || base.CanConvertFrom(context, sourceType);
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
                return ParseFillBrush(stringValue);
            else
                return base.ConvertFrom(context, culture, value);
        }

        public static FillBrush ParseFillBrush(string Value)
        {
            string[] colorStrings = Value.Split('|');
            if (colorStrings.Length == 1)
            {
                XAMLColor Color = ColorStringConverter.ParseColor(colorStrings[0]);
                return new SolidFillBrush(Color);
            }
            else if (colorStrings.Length == 2)
            {
                XAMLColor Color1 = ColorStringConverter.ParseColor(colorStrings[0]);
                XAMLColor Color2 = ColorStringConverter.ParseColor(colorStrings[1]);
                XNAColor Lerped = XNAColor.Lerp(Color1.ToXNAColor(), Color2.ToXNAColor(), 0.5f);
                XAMLColor Diagonals = new XAMLColor(Lerped.R, Lerped.G, Lerped.B, Lerped.A);
                return new GradientFillBrush(Color1, Diagonals, Color2, Diagonals);
            }
            else if (colorStrings.Length == 4)
            {
                XAMLColor[] Colors = colorStrings.Select(x => ColorStringConverter.ParseColor(x)).ToArray();
                return new GradientFillBrush(Colors[0], Colors[1], Colors[2], Colors[3]);
            }
            else
                throw new InvalidOperationException($"{Value} is not a valid format for a {nameof(FillBrush)}.");
        }
    }

    public class SolidFillBrush : FillBrush
    {
        public XAMLColor Color { get; set; }

        public SolidFillBrush() : this(new XAMLColor()) { }
        public SolidFillBrush(XAMLColor Color)
        {
            this.Color = Color;
        }

        public override string ToString() => $"{nameof(SolidFillBrush)}: {Color}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => new MGSolidFillBrush(Color.ToXNAColor());
    }

    public class GradientFillBrush : FillBrush
    {
        public XAMLColor TopLeftColor { get; set; }
        public XAMLColor TopRightColor { get; set; }
        public XAMLColor BottomLeftColor { get; set; }
        public XAMLColor BottomRightColor { get; set; }

        public GradientFillBrush() : this(new XAMLColor(), new XAMLColor(), new XAMLColor(), new XAMLColor()) { }
        public GradientFillBrush(XAMLColor TopLeft, XAMLColor TopRight, XAMLColor BottomRight, XAMLColor BottomLeft)
        {
            TopLeftColor = TopLeft;
            TopRightColor = TopRight;
            BottomRightColor = BottomRight;
            BottomLeftColor = BottomLeft;
        }

        public override string ToString() => $"{nameof(GradientFillBrush)}: {TopLeftColor} {TopRightColor} {BottomRightColor} {BottomLeftColor}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => new MGGradientFillBrush(TopLeftColor.ToXNAColor(), TopRightColor.ToXNAColor(), BottomRightColor.ToXNAColor(), BottomLeftColor.ToXNAColor());
    }

    public class DiagonalGradientFillBrush : FillBrush
    {
        public XAMLColor Color1 { get; set; }
        public XAMLColor Color2 { get; set; }
        public CornerType Color1Position { get; set; } = CornerType.TopLeft;

        public DiagonalGradientFillBrush() : this(new XAMLColor(), new XAMLColor(), CornerType.TopLeft) { }
        public DiagonalGradientFillBrush(XAMLColor Color1, XAMLColor Color2, CornerType Color1Position)
        {
            this.Color1 = Color1;
            this.Color2 = Color2;
            this.Color1Position = Color1Position;
        }

        public override string ToString() => $"{nameof(DiagonalGradientFillBrush)}: {Color1} ({Color1Position}) / {Color2}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => new MGDiagonalGradientFillBrush(Color1.ToXNAColor(), Color2.ToXNAColor(), Color1Position);
    }

    public class TextureFillBrush : FillBrush
    {
        public string SourceName { get; set; }
        public Stretch Stretch { get; set; } = Stretch.Fill;
        public XAMLColor Color { get; set; } = new XAMLColor();

        public TextureFillBrush() : this(null, Stretch.Fill, new XAMLColor()) { }
        public TextureFillBrush(string SourceName, Stretch Stretch, XAMLColor Color)
        {
            this.SourceName = SourceName;
            this.Stretch = Stretch;
            this.Color = Color;
        }

        public override string ToString() => $"{nameof(TextureFillBrush)}: {SourceName} ({Stretch})";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => new MGTextureFillBrush(Desktop, SourceName, Stretch, Color.ToXNAColor());
    }

    public class EffectParameter
    {
        public string Name { get; set; }
        public MGEffectParameterType? Type { get; set; }
        public string Value { get; set; }

        internal MGEffectParameterValue ToParameterValue()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException($"{nameof(EffectParameter)}.{nameof(Name)} cannot be null or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Value))
            {
                throw new InvalidOperationException($"{nameof(EffectParameter)} '{Name}' must specify a value.");
            }

            MGEffectParameterType ActualType = Type ?? InferType(Value);
            try
            {
                object ParsedValue = ParseValue(Value, ActualType);
                ValidateFinite(Name, ActualType, ParsedValue);
                return new MGEffectParameterValue(Name, ActualType, ParsedValue);
            }
            catch (Exception Ex) when (Ex is FormatException or OverflowException or ArgumentException)
            {
                throw new FormatException(
                    $"Effect parameter '{Name}' declared as {ActualType} has invalid value '{Value}'. Expected {GetExpectedFormat(ActualType)}.", Ex);
            }
        }

        private static MGEffectParameterType InferType(string Value)
        {
            if (Value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) || Value.StartsWith("#", StringComparison.Ordinal))
            {
                return MGEffectParameterType.Color;
            }

            if (bool.TryParse(Value, out _))
            {
                return MGEffectParameterType.Bool;
            }

            if (int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return MGEffectParameterType.Int;
            }

            if (float.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return MGEffectParameterType.Float;
            }

            throw new FormatException($"Cannot infer a supported scalar or color effect parameter type from '{Value}'. Vector values require an explicit {nameof(Type)}.");
        }

        private static object ParseValue(string Value, MGEffectParameterType Type)
        {
            try
            {
                if (Type == MGEffectParameterType.Color)
                {
                    return ColorStringConverter.ParseColor(Value).ToXNAColor().ToVector4();
                }

                return Type switch
                {
                    MGEffectParameterType.Float => float.Parse(Value, CultureInfo.InvariantCulture),
                    MGEffectParameterType.Int => int.Parse(Value, CultureInfo.InvariantCulture),
                    MGEffectParameterType.Bool => bool.Parse(Value),
                    MGEffectParameterType.Color => throw new FormatException($"'{Value}' is not a valid MGUI color."),
                    MGEffectParameterType.Vector2 => ParseVector2(Value),
                    MGEffectParameterType.Vector3 => ParseVector3(Value),
                    MGEffectParameterType.Vector4 => ParseVector4(Value),
                    _ => throw new FormatException($"'{Value}' is not a valid {Type} value.")
                };
            }
            catch (Exception Ex) when (Ex is FormatException or OverflowException)
            {
                throw new FormatException($"Effect parameter value '{Value}' is not valid for type {Type}.", Ex);
            }
        }

        private static float[] ParseComponents(string Value, int ExpectedCount)
        {
            float[] Components = Value.Split(',').Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (Components.Length != ExpectedCount)
            {
                throw new FormatException($"Expected {ExpectedCount} comma-separated components.");
            }

            return Components;
        }

        private static Vector2 ParseVector2(string Value)
        {
            float[] Components = ParseComponents(Value, 2);
            return new Vector2(Components[0], Components[1]);
        }

        private static Vector3 ParseVector3(string Value)
        {
            float[] Components = ParseComponents(Value, 3);
            return new Vector3(Components[0], Components[1], Components[2]);
        }

        private static Vector4 ParseVector4(string Value)
        {
            float[] Components = ParseComponents(Value, 4);
            return new Vector4(Components[0], Components[1], Components[2], Components[3]);
        }

        private static void ValidateFinite(string Name, MGEffectParameterType Type, object Value)
        {
            bool IsFinite = Type switch
            {
                MGEffectParameterType.Float => float.IsFinite((float)Value),
                MGEffectParameterType.Vector2 => IsFiniteVector((Vector2)Value),
                MGEffectParameterType.Vector3 => IsFiniteVector((Vector3)Value),
                MGEffectParameterType.Vector4 or MGEffectParameterType.Color => IsFiniteVector((Vector4)Value),
                _ => true
            };
            if (!IsFinite)
            {
                throw new FormatException($"Effect parameter '{Name}' must contain only finite numeric values.");
            }
        }

        private static bool IsFiniteVector(Vector2 Value) => float.IsFinite(Value.X) && float.IsFinite(Value.Y);
        private static bool IsFiniteVector(Vector3 Value) => float.IsFinite(Value.X) && float.IsFinite(Value.Y) && float.IsFinite(Value.Z);
        private static bool IsFiniteVector(Vector4 Value) => float.IsFinite(Value.X) && float.IsFinite(Value.Y) && float.IsFinite(Value.Z) && float.IsFinite(Value.W);

        private static string GetExpectedFormat(MGEffectParameterType Type)
            => Type switch
            {
                MGEffectParameterType.Float => "a finite invariant-culture floating-point scalar such as 1.25",
                MGEffectParameterType.Int => "an invariant-culture integer such as 2",
                MGEffectParameterType.Bool => "true or false",
                MGEffectParameterType.Color => "an MGUI color such as rgb(210,165,72) or #D2A548",
                MGEffectParameterType.Vector2 => "two finite comma-separated components such as 1,0",
                MGEffectParameterType.Vector3 => "three finite comma-separated components such as 1,0,0",
                MGEffectParameterType.Vector4 => "four finite comma-separated components such as 1,0,0,1",
                _ => "one of Float, Int, Bool, Color, Vector2, Vector3, or Vector4"
            };
    }

    internal static class EffectParameterCollectionConverter
    {
        public static MGEffectParameterValue[] Convert(
            IReadOnlyList<EffectParameter> Parameters,
            string OwnerDescription)
        {
            if (Parameters == null)
            {
                throw new InvalidOperationException(
                    $"The effect parameter collection in {OwnerDescription} cannot be null.");
            }

            HashSet<string> ParameterNames = new(StringComparer.Ordinal);
            MGEffectParameterValue[] ConvertedParameters = new MGEffectParameterValue[Parameters.Count];
            for (int i = 0; i < Parameters.Count; i++)
            {
                EffectParameter Parameter = Parameters[i];
                if (Parameter == null)
                {
                    throw new InvalidOperationException(
                        $"Effect parameter declaration at index {i} in {OwnerDescription} cannot be null.");
                }

                MGEffectParameterValue ConvertedParameter = Parameter.ToParameterValue();
                if (!ParameterNames.Add(ConvertedParameter.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate effect parameter name '{ConvertedParameter.Name}' in {OwnerDescription}. Names are case-sensitive and must be unique.");
                }

                ConvertedParameters[i] = ConvertedParameter;
            }

            return ConvertedParameters;
        }
    }

    [ContentProperty(nameof(Parameters))]
    public class EffectFillBrush : FillBrush
    {
        public string EffectName { get; set; }
        public bool UseStandardParameters { get; set; } = false;
        public FillBrush FallbackBrush { get; set; }
        public List<EffectParameter> Parameters { get; set; } = new();

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element)
        {
            if (Desktop == null)
            {
                throw new ArgumentNullException(nameof(Desktop));
            }

            if (string.IsNullOrWhiteSpace(EffectName))
            {
                throw new ArgumentException($"{nameof(EffectFillBrush)}.{nameof(EffectName)} cannot be null, empty, or whitespace.", nameof(EffectName));
            }

            MGEffectParameterValue[] ConvertedParameters = EffectParameterCollectionConverter.Convert(
                Parameters,
                $"{nameof(EffectFillBrush)} '{EffectName}'");

            if (!Desktop.Resources.TryGetEffect(EffectName, out Microsoft.Xna.Framework.Graphics.Effect Effect))
            {
                string Message = $"No Effect was found with the name '{EffectName}' in {nameof(MGResources)}.{nameof(MGResources.Effects)}.";
                if (FallbackBrush != null)
                {
                    return FallbackBrush.ToFillBrush(Desktop, Element);
                }

                throw new InvalidOperationException($"{Message} Register it with {nameof(MGResources)}.{nameof(MGResources.AddEffect)} " +
                    $"before XAML brush materialization, or specify {nameof(FallbackBrush)} as a non-shader alternative.");
            }

            return new MGEffectFillBrush(Effect)
            {
                UseStandardParameters = UseStandardParameters,
                Parameters = ConvertedParameters
            };
        }

        public override string ToString() => $"{nameof(EffectFillBrush)}: {EffectName}";

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
            {
                yield return Item;
            }

            yield return (FallbackBrush, nameof(FallbackBrush));
        }
    }

    [ContentProperty(nameof(FillBrush))]
    public class BorderedFillBrush : FillBrush
    {
        public Thickness BorderThickness { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public FillBrush FillBrush { get; set; }
        public bool PadFillBoundsByBorderThickness { get; set; } = false;

        public BorderedFillBrush() : this(new Thickness(0), null, null, false) { }
        public BorderedFillBrush(Thickness BorderThickness, BorderBrush BorderBrush, FillBrush FillBrush, bool PadFillBoundsByBorderThickness)
        {
            this.BorderThickness = BorderThickness;
            this.BorderBrush = BorderBrush;
            this.FillBrush = FillBrush;
            this.PadFillBoundsByBorderThickness = PadFillBoundsByBorderThickness;
        }

        public override string ToString() => $"{nameof(BorderedFillBrush)}: {BorderBrush} / {FillBrush}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => 
            new MGBorderedFillBrush(BorderThickness.ToThickness(), BorderBrush?.ToBorderBrush(Desktop, Element), FillBrush?.ToFillBrush(Desktop, Element), PadFillBoundsByBorderThickness);

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            yield return (BorderBrush, nameof(BorderBrush));
            yield return (FillBrush, nameof(FillBrush));
        }
    }

    [ContentProperty(nameof(Brushes))]
    public class CompositedFillBrush : FillBrush
    {
        public List<FillBrush> Brushes { get; set; } = new();

        public CompositedFillBrush() : this(new List<FillBrush>()) { }
        public CompositedFillBrush(IEnumerable<FillBrush> Brushes)
        {
            this.Brushes = Brushes.ToList();
        }

        public override string ToString() => $"{nameof(CompositedFillBrush)}: {Brushes.Count} brush(es)";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) => new MGCompositedFillBrush(Brushes.Select(x => x.ToFillBrush(Desktop, Element)).ToArray());

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            foreach (FillBrush Brush in Brushes)
                yield return (Brush, nameof(Brushes));
        }
    }

    [ContentProperty(nameof(Brush))]
    public class PaddedFillBrush : FillBrush
    {
        public FillBrush Brush { get; set; }
        public Thickness Padding { get; set; }

        public float? Scale { get; set; }

        public int? MinWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxWidth { get; set; }
        public int? MaxHeight { get; set; }

        public HorizontalAlignment? HorizontalAlignment { get; set; }
        public VerticalAlignment? VerticalAlignment { get; set; }

        public PaddedFillBrush() : this(null, new(0), null, null, null, null, null, null, null) { }
        public PaddedFillBrush(FillBrush Brush, Thickness Padding, float? Scale, int? MinWidth, int? MinHeight, int? MaxWidth, int? MaxHeight, 
            HorizontalAlignment? HorizontalAlignment, VerticalAlignment? VerticalAlignment)
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

        public override string ToString() => $"{nameof(PaddedFillBrush)}: {Brush}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element) 
            => new MGPaddedFillBrush(Brush?.ToFillBrush(Desktop, Element), Padding.ToThickness(), Scale, MinWidth, MinHeight, MaxWidth, MaxHeight, HorizontalAlignment, VerticalAlignment);

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            yield return (Brush, nameof(Brush));
        }
    }

    public class HighlightFillBrush : FillBrush
    {
        public bool IsEnabled { get; set; } = true;
        public bool FillFocusedRegion { get; set; } = false;
        public XAMLColor? FocusedColor { get; set; }
        public bool FillUnfocusedRegion { get; set; } = true;
        public XAMLColor? UnfocusedColor { get; set; }
        public IReadOnlyList<Microsoft.Xna.Framework.Rectangle> FocusedBounds { get; set; }
        public IReadOnlyList<MGElement> FocusedElements { get; set; }
        public MGElement FocusedElement { get; set; }
        public int FocusedElementPadding { get; set; }

        public override string ToString() => $"{nameof(HighlightFillBrush)}: {FocusedColor} / {UnfocusedColor}";

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element)
        {
            MGHighlightFillBrush Brush = new(FillFocusedRegion, FocusedColor?.ToXNAColor(), FillUnfocusedRegion, UnfocusedColor?.ToXNAColor())
            {
                IsEnabled = IsEnabled,
                FocusedBounds = FocusedBounds,
                FocusedElements = FocusedElements,
                FocusedElementPadding = FocusedElementPadding,
                SourceElement = Element
            };
            return Brush;
        }
    }

    [ContentProperty(nameof(Parameters))]
    public class NineSliceFillBrush : FillBrush
    {
        public string SourceName { get; set; }
        public Thickness? SourceMargin { get; set; }
        public FillBrush InteriorBrush { get; set; }
        public string EffectName { get; set; }
        public bool UseStandardParameters { get; set; } = false;
        public List<EffectParameter> Parameters { get; set; } = new();

        /// <summary>The unscaled UI thickness used for destination slices. Rendering scales this through border UI scaling.</summary>
        public Thickness TargetMargin { get; set; }

        public override IFillBrush ToFillBrush(MGDesktop Desktop, MGElement Element)
        {
            Microsoft.Xna.Framework.Graphics.Effect Effect = null;
            MGEffectParameterValue[] ConvertedParameters = Array.Empty<MGEffectParameterValue>();
            if (EffectName == null)
            {
                if (UseStandardParameters)
                {
                    throw new InvalidOperationException(
                        $"{nameof(NineSliceFillBrush)}.{nameof(UseStandardParameters)} requires a nonblank " +
                        $"{nameof(NineSliceFillBrush)}.{nameof(EffectName)}.");
                }

                if (Parameters?.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"{nameof(NineSliceFillBrush)}.{nameof(Parameters)} cannot contain declarations without a nonblank " +
                        $"{nameof(NineSliceFillBrush)}.{nameof(EffectName)}.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(EffectName))
                {
                    throw new ArgumentException(
                        $"{nameof(NineSliceFillBrush)}.{nameof(EffectName)} cannot be empty or whitespace.",
                        nameof(EffectName));
                }

                ConvertedParameters = EffectParameterCollectionConverter.Convert(
                    Parameters,
                    $"{nameof(NineSliceFillBrush)} '{EffectName}'");

                if (!Desktop.Resources.TryGetEffect(EffectName, out Effect))
                {
                    throw new InvalidOperationException(
                        $"No Effect was found with the name '{EffectName}' in {nameof(MGResources)}.{nameof(MGResources.Effects)}. " +
                        $"Register it with {nameof(MGResources)}.{nameof(MGResources.AddEffect)} before XAML brush materialization.");
                }
            }

            if (SourceName == null)
            {
                throw new ArgumentNullException(nameof(SourceName));
            }

            if (!Desktop.Resources.TryGetTexture(SourceName, out MGTextureData Source))
            {
                throw new InvalidOperationException($"No Texture was found with the name '{SourceName}' in {nameof(MGResources)}.{nameof(MGResources.Textures)}.");
            }

            IFillBrush MaterializedInteriorBrush = InteriorBrush?.ToFillBrush(Desktop, Element);
            if (Effect == null)
            {
                return new MGNineSliceFillBrush(
                    TargetMargin.ToThickness(),
                    Source,
                    SourceMargin?.ToThickness(),
                    MaterializedInteriorBrush);
            }

            return new MGNineSliceFillBrush(
                Effect,
                TargetMargin.ToThickness(),
                Source,
                SourceMargin?.ToThickness(),
                MaterializedInteriorBrush)
            {
                UseStandardParameters = UseStandardParameters,
                Parameters = ConvertedParameters
            };
        }

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            yield return (InteriorBrush, nameof(InteriorBrush));
        }
    }
    #endregion Fill Brush

    #region Border Brush
    [TypeConverter(typeof(BorderBrushStringConverter))]
    public abstract class BorderBrush : XAMLBindableBase
    {
        public abstract IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element);
    }

    public class BorderBrushStringConverter : TypeConverter
    {
        private readonly FillBrushStringConverter FillBrushStringConverter = new();
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => FillBrushStringConverter.CanConvertFrom(context, sourceType) || base.CanConvertFrom(context, sourceType);
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                string[] fillBrushStrings = stringValue.Split('-');
                if (fillBrushStrings.Length == 1)
                {
                    FillBrush FillBrush = (FillBrush)FillBrushStringConverter.ConvertFrom(context, culture, fillBrushStrings[0]);
                    return new UniformBorderBrush(FillBrush);
                }
                else if (fillBrushStrings.Length is 2 or 4)
                {
                    FillBrush[] FillBrushes = fillBrushStrings.Select(x => (FillBrush)FillBrushStringConverter.ConvertFrom(context, culture, x)).ToArray();
                    if (FillBrushes.Length == 2)
                        return new DockedBorderBrush(FillBrushes[0], FillBrushes[1], FillBrushes[1], FillBrushes[0]);
                    else
                        return new DockedBorderBrush(FillBrushes[0], FillBrushes[1], FillBrushes[2 % FillBrushes.Length], FillBrushes[3 % FillBrushes.Length]);
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }

    [ContentProperty(nameof(Brush))]
    public class UniformBorderBrush : BorderBrush
    {
        public FillBrush Brush { get; set; }

        public UniformBorderBrush() : this(null) { }
        public UniformBorderBrush(FillBrush Brush)
        {
            this.Brush = Brush;
        }

        public override string ToString() => $"{nameof(UniformBorderBrush)}: {Brush}";

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element) => new MGUniformBorderBrush(Brush.ToFillBrush(Desktop, Element));
    }

    public class DockedBorderBrush : BorderBrush
    {
        public FillBrush Left { get; set; }
        public FillBrush Top { get; set; }
        public FillBrush Right { get; set; }
        public FillBrush Bottom { get; set; }

        public DockedBorderBrush() : this(null, null, null, null) { }
        public DockedBorderBrush(FillBrush Left, FillBrush Top, FillBrush Right, FillBrush Bottom)
        {
            this.Left = Left;
            this.Top = Top;
            this.Right = Right;
            this.Bottom = Bottom;
        }

        public override string ToString() => $"{nameof(DockedBorderBrush)}: {Left}, {Top}, {Right}, {Bottom}";

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element) => 
            new MGDockedBorderBrush(Left.ToFillBrush(Desktop, Element), Top.ToFillBrush(Desktop, Element), Right.ToFillBrush(Desktop, Element), Bottom.ToFillBrush(Desktop, Element));

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            yield return (Left, nameof(Left));
            yield return (Top, nameof(Top));
            yield return (Right, nameof(Right));
            yield return (Bottom, nameof(Bottom));
        }
    }

    [ContentProperty(nameof(Bands))]
    public class BandedBorderBrush : BorderBrush
    {
        public List<BorderBand> Bands { get; set; } = new();

        public BandedBorderBrush() : this(new List<BorderBand>()) { }
        public BandedBorderBrush(IList<BorderBand> Bands)
        {
            this.Bands = Bands.ToList();
        }

        public override string ToString() => $"{nameof(BandedBorderBrush)}: {Bands.Count} band(s)";

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element) => new MGBandedBorderBrush(Bands.Select(x => x.ToBorderBand(Desktop, Element)).ToArray());
    }

    [ContentProperty(nameof(Brush))]
    public class BorderBand
    {
        public BorderBrush Brush { get; set; } = new UniformBorderBrush(new SolidFillBrush(new XAMLColor(255, 255, 255, 0)));
        public double ThicknessWeight { get; set; } = 1.0;

        public MGBorderBand ToBorderBand(MGDesktop Desktop, MGElement Element) => new(Brush.ToBorderBrush(Desktop, Element), ThicknessWeight);
    }

    public class TexturedBorderBrush : BorderBrush
    {
        public string EdgeTextureName { get; set; }
        public XAMLColor? EdgeColor { get; set; }
        public string CornerTextureName { get; set; }
        public XAMLColor? CornerColor { get; set; }
        public float Opacity { get; set; } = 1.0f;

        public Edge? EdgeBasis { get; set; } = Edge.Left;
        public Corner? CornerBasis { get; set; } = Corner.TopLeft;
        private TextureTransforms Transforms => EdgeBasis.HasValue && CornerBasis.HasValue ? TextureTransforms.CreateStandardRotated(EdgeBasis.Value, CornerBasis.Value) : new();

        public TexturedBorderBrush() : this(null, null, null, null, 1.0f) { }
        public TexturedBorderBrush(string EdgeTextureName, XAMLColor? EdgeColor, string CornerTextureName, XAMLColor? CornerColor, float Opacity)
        {
            this.EdgeTextureName = EdgeTextureName;
            this.EdgeColor = EdgeColor;
            this.CornerTextureName = CornerTextureName;
            this.CornerColor = CornerColor;
            this.Opacity = Opacity;
        }

        public override string ToString() => $"{nameof(TexturedBorderBrush)}: {EdgeTextureName} / {CornerTextureName}";

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element)
            => new MGTexturedBorderBrush(Desktop, EdgeTextureName, CornerTextureName, EdgeColor?.ToXNAColor(), CornerColor?.ToXNAColor(), Transforms, Opacity);
    }

    [ContentProperty(nameof(Underlay))]
    public class HighlightBorderBrush : BorderBrush
    {
        public BorderBrush Underlay { get; set; }
        public XAMLColor? HighlightColor { get; set; }
        public HighlightAnimation? AnimationType { get; set; }

        public double? AnimationProgress { get; set; }

        public bool? IsEnabled { get; set; }

        public TimeSpan? PulseFadeDuration { get; set; }
        public TimeSpan? PulseDelay { get; set; }

        public TimeSpan? FlashShowDuration { get; set; }
        public TimeSpan? FlashHideDuration { get; set; }

        public HighlightFlowDirection? ProgressFlowDirection { get; set; }
        public TimeSpan? ProgressDuration { get; set; }
        public double? ProgressSize { get; set; }

        public Orientation? ScanOrientation { get; set; }
        public bool? ScanIsReversed { get; set; }
        public TimeSpan? ScanDuration { get; set; }
        public double? ScanSize { get; set; }

        public bool? StopOnMouseOver { get; set; }
        public bool? StopOnClick { get; set; }

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element)
        {
            MGElement ActualElement = Element is MGBorder && Element.IsComponent ? Element.ComponentParent : Element;
            MGHighlightBorderBrush Brush = new MGHighlightBorderBrush(Underlay?.ToBorderBrush(Desktop, ActualElement), HighlightColor?.ToXNAColor() ?? XNAColor.Yellow, 
                AnimationType ?? HighlightAnimation.Pulse, ActualElement);

            if (AnimationProgress.HasValue)
                Brush.AnimationProgress = AnimationProgress.Value;

            if (IsEnabled.HasValue)
                Brush.IsEnabled = IsEnabled.Value;

            if (PulseFadeDuration.HasValue)
                Brush.PulseFadeDuration = PulseFadeDuration.Value;
            if (PulseDelay.HasValue)
                Brush.PulseDelay = PulseDelay.Value;

            if (FlashShowDuration.HasValue)
                Brush.FlashShowDuration = FlashShowDuration.Value;
            if (FlashHideDuration.HasValue)
                Brush.FlashHideDuration = FlashHideDuration.Value;

            if (ProgressFlowDirection.HasValue)
                Brush.ProgressFlowDirection = ProgressFlowDirection.Value;
            if (ProgressDuration.HasValue)
                Brush.ProgressDuration = ProgressDuration.Value;
            if (ProgressSize.HasValue)
                Brush.ProgressSize = ProgressSize.Value;

            if (ScanOrientation.HasValue)
                Brush.ScanOrientation = ScanOrientation.Value;
            if (ScanIsReversed.HasValue)
                Brush.ScanIsReversed = ScanIsReversed.Value;
            if (ScanDuration.HasValue)
                Brush.ScanDuration = ScanDuration.Value;
            if (ScanSize.HasValue)
                Brush.ScanSize = ScanSize.Value;

            if (StopOnMouseOver.HasValue)
                Brush.StopOnMouseOver = StopOnMouseOver.Value;
            if (StopOnClick.HasValue)
                Brush.StopOnClick = StopOnClick.Value;

            return Brush;
        }

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            yield return (Underlay, nameof(Underlay));
        }
    }

    [ContentProperty(nameof(Brushes))]
    public class CompositedBorderBrush : BorderBrush
    {
        public List<BorderBrush> Brushes { get; set; } = new();

        public CompositedBorderBrush() : this(new List<BorderBrush>()) { }
        public CompositedBorderBrush(IEnumerable<BorderBrush> Brushes)
        {
            this.Brushes = Brushes.ToList();
        }

        public override string ToString() => $"{nameof(CompositedBorderBrush)}: {Brushes.Count} brush(es)";

        public override IBorderBrush ToBorderBrush(MGDesktop Desktop, MGElement Element) => new MGCompositedBorderBrush(Brushes.Select(x => x.ToBorderBrush(Desktop, Element)).ToArray());

        protected internal override IEnumerable<(XAMLBindableBase, string)> GetNestedBindableObjects()
        {
            foreach (var Item in base.GetNestedBindableObjects())
                yield return Item;
            foreach (BorderBrush Brush in Brushes)
                yield return (Brush, nameof(Brushes));
        }
    }
    #endregion Border Brush
}
