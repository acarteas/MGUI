using Microsoft.Xna.Framework;
using System.Collections;
using System.Reflection;
using Portable.Xaml;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using XamlEffectParameter = MGUI.Core.UI.XAML.EffectParameter;

namespace MGUI.Tests.XAML;

public class ShaderEffectsSampleTests
{
    [Fact]
    public void SampleXamlParsesAndDemonstratesRequiredFeatures()
    {
        string Source = ReadResource("ShaderEffects.xaml");

        Window Root = Assert.IsType<Window>(XamlServices.Parse(Source));
        IReadOnlyList<FillBrush> SharedEffectBrushes = GetSharedEffectBrushes(Root);

        Assert.Equal(8, SharedEffectBrushes.Count);
        Assert.Contains("UseStandardParameters=\"True\"", Source);
        Assert.Contains("<EffectParameter Name=\"AccentColor\" Type=\"Color\"", Source);
        Assert.Contains("<EffectParameter Name=\"ButtonRole\" Type=\"Float\"", Source);
        Assert.Equal(2, CountOccurrences(Source, "<NineSliceFillBrush "));
        Assert.Contains("SourceName=\"ShaderOrnamentalFrame\"", Source);
        Assert.Contains("SourceMargin=\"52\"", Source);
        Assert.Contains("TargetMargin=\"24\"", Source);
        Assert.Contains("<EffectParameter Name=\"TreatmentDirection\" Type=\"Vector2\" Value=\"1,0.65\"", Source);
        Assert.Contains("<EffectParameter Name=\"TreatmentStrength\" Type=\"Float\" Value=\"0.75\"", Source);
        Assert.Contains("<NineSliceFillBrush.InteriorBrush>", Source);
        Assert.Contains("The woven center is sampled and shaded.", Source);
        Assert.Contains("Only the eight texture-backed frame slices are shaded.", Source);
        Assert.Contains("<Button.DisabledBackground>", Source);
        Assert.Contains("<ToggleButton.SelectedBackground>", Source);
        Assert.Contains("HoveredTextureColor=", Source);
        Assert.Contains("PressedTextureColor=", Source);
        Assert.Contains("HoveredRenderScale=", Source);
        Assert.Contains("PressedRenderScale=", Source);
        Assert.Contains("PressedContentOffset=", Source);
        Assert.Contains("ToolTip=", Source);
    }

    [Fact]
    public void SharedSampleEffectDeclarationsRestoreCompleteCustomState()
    {
        Window Root = Assert.IsType<Window>(XamlServices.Parse(ReadResource("ShaderEffects.xaml")));
        IReadOnlyList<FillBrush> SharedEffectBrushes = GetSharedEffectBrushes(Root);
        EffectFillBrush[] RectangularBrushes = SharedEffectBrushes.OfType<EffectFillBrush>().ToArray();
        NineSliceFillBrush[] NineSliceBrushes = SharedEffectBrushes.OfType<NineSliceFillBrush>().ToArray();

        Assert.Equal(6, RectangularBrushes.Length);
        Assert.Equal(2, NineSliceBrushes.Length);

        foreach (EffectFillBrush Brush in RectangularBrushes)
        {
            AssertCompleteParameterSet(Brush.Parameters);
            AssertParameter(Brush.Parameters, "TreatmentDirection", MGEffectParameterType.Vector2, "0,0");
            AssertParameter(Brush.Parameters, "TreatmentStrength", MGEffectParameterType.Float, "0");
        }

        foreach (NineSliceFillBrush Brush in NineSliceBrushes)
        {
            AssertCompleteParameterSet(Brush.Parameters);
            AssertParameter(Brush.Parameters, "ButtonRole", MGEffectParameterType.Float, "0");
        }
    }

    [Fact]
    public void SampleRegistersResourcesBeforeMaterializationWithoutElementLookups()
    {
        string Source = ReadResource("ShaderEffects.xaml.cs");

        Assert.Contains("\"ShaderEffects.xaml\", () => RegisterShaderResources(Content, Desktop.Resources)", Source);
        Assert.Contains("AddOrReplaceEffect", Source);
        Assert.Contains("Path.Combine(\"Brush Textures\", \"9SliceTexture-1\")", Source);
        Assert.Contains("AddTexture(OrnamentalFrameTextureName", Source);
        Assert.DoesNotContain("GetElementByName", Source);
    }

    [Fact]
    public void SampleShaderAppliesOpacityOnce()
    {
        string Source = ReadResource("UiEffects.fx");

        Assert.Contains("input.Color * result", Source);
        Assert.Contains("sampler2D SpriteTextureSampler : register(s0);", Source);
        Assert.Contains("tex2D(SpriteTextureSampler, textureCoordinate)", Source);
        Assert.DoesNotContain("result.a *= Opacity", Source);
        Assert.Contains("HoverAmount", Source);
        Assert.Contains("PressAmount", Source);
        Assert.Contains("SelectedAmount", Source);
        Assert.Contains("DisabledAmount", Source);
    }

    [Fact]
    public void SampleShaderUsesTextureCoordinatesForLocalUv()
    {
        string Source = ReadResource("UiEffects.fx");

        Assert.Contains("float2 textureCoordinate = input.TextureCoordinate;", Source);
        Assert.Contains("textureCoordinate * ElementTextureCoordinateScale + ElementTextureCoordinateOffset", Source);
        Assert.Contains("TreatmentDirection", Source);
        Assert.Contains("TreatmentStrength", Source);
        Assert.DoesNotContain("saturate((input.Position.xy - ElementPosition) /", Source);
    }

    [Fact]
    public void TextureCoordinateInterpolationPreservesEdgeDistanceAcrossAffineTransform()
    {
        Vector2 elementPosition = new(37f, 53f);
        Vector2 elementSize = new(180f, 72f);
        Vector2 localUv = new(0.23f, 0.71f);

        Vector2 topLeftPosition = elementPosition;
        Vector2 topRightPosition = elementPosition + new Vector2(elementSize.X, 0f);
        Vector2 bottomLeftPosition = elementPosition + new Vector2(0f, elementSize.Y);
        Vector2 bottomRightPosition = elementPosition + elementSize;

        Matrix transform = Matrix.CreateScale(1.5f, 0.65f, 1f)
            * Matrix.CreateTranslation(240f, -35f, 0f);

        Vector2 transformedTopLeft = Vector2.Transform(topLeftPosition, transform);
        Vector2 transformedTopRight = Vector2.Transform(topRightPosition, transform);
        Vector2 transformedBottomLeft = Vector2.Transform(bottomLeftPosition, transform);
        Vector2 transformedBottomRight = Vector2.Transform(bottomRightPosition, transform);

        Vector2 originalPosition = BilinearInterpolate(
            topLeftPosition,
            topRightPosition,
            bottomLeftPosition,
            bottomRightPosition,
            localUv);
        Vector2 transformedPosition = BilinearInterpolate(
            transformedTopLeft,
            transformedTopRight,
            transformedBottomLeft,
            transformedBottomRight,
            localUv);
        Vector2 interpolatedUv = BilinearInterpolate(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            localUv);

        Vector2 legacyMixedSpaceUv = (transformedPosition - elementPosition) / elementSize;

        Assert.NotEqual(originalPosition, transformedPosition);
        Assert.NotEqual(localUv, legacyMixedSpaceUv);
        Assert.True(legacyMixedSpaceUv.X > 1f || legacyMixedSpaceUv.X < 0f
            || legacyMixedSpaceUv.Y > 1f || legacyMixedSpaceUv.Y < 0f);
        Assert.Equal(localUv.X, interpolatedUv.X, precision: 5);
        Assert.Equal(localUv.Y, interpolatedUv.Y, precision: 5);

        float originalDistance = EdgeDistance(localUv, elementSize);
        float transformedDistance = EdgeDistance(interpolatedUv, elementSize);
        Assert.Equal(originalDistance, transformedDistance, precision: 5);
    }

    private static string ReadResource(string Suffix)
    {
        Assembly Assembly = typeof(ShaderEffectsSampleTests).Assembly;
        string Name = Assert.Single(Assembly.GetManifestResourceNames().Where(x => x.EndsWith(Suffix, StringComparison.Ordinal)));
        using Stream Stream = Assert.IsAssignableFrom<Stream>(Assembly.GetManifestResourceStream(Name));
        using StreamReader Reader = new(Stream);
        return Reader.ReadToEnd();
    }

    private static float EdgeDistance(Vector2 uv, Vector2 elementSize)
    {
        Vector2 edge = Vector2.Min(uv, Vector2.One - uv);
        return MathF.Min(edge.X * elementSize.X, edge.Y * elementSize.Y);
    }

    private static IReadOnlyList<FillBrush> GetSharedEffectBrushes(Window Root)
    {
        HashSet<Element> VisitedElements = new(ReferenceEqualityComparer.Instance);
        HashSet<FillBrush> VisitedBrushes = new(ReferenceEqualityComparer.Instance);
        List<FillBrush> Result = new();
        Stack<Element> PendingElements = new();
        PendingElements.Push(Root);

        while (PendingElements.TryPop(out Element? Element))
        {
            if (!VisitedElements.Add(Element))
            {
                continue;
            }

            foreach (PropertyInfo Property in Element.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (Property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object? Value = Property.GetValue(Element);
                if (Value is Element Child)
                {
                    PendingElements.Push(Child);
                }
                else if (Value is IEnumerable Children)
                {
                    foreach (Element NestedChild in Children.OfType<Element>())
                    {
                        PendingElements.Push(NestedChild);
                    }
                }

                if (Value is FillBrush Brush
                    && VisitedBrushes.Add(Brush)
                    && GetEffectName(Brush) == "SampleUiEffect")
                {
                    Result.Add(Brush);
                }
            }
        }

        return Result;
    }

    private static string? GetEffectName(FillBrush Brush)
        => Brush switch
        {
            EffectFillBrush EffectBrush => EffectBrush.EffectName,
            NineSliceFillBrush NineSliceBrush => NineSliceBrush.EffectName,
            _ => null
        };

    private static void AssertCompleteParameterSet(IReadOnlyList<XamlEffectParameter> Parameters)
    {
        string[] ExpectedNames =
        {
            "AccentColor",
            "ButtonRole",
            "TreatmentDirection",
            "TreatmentStrength"
        };

        Assert.Equal(ExpectedNames, Parameters.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        AssertParameterType(Parameters, "AccentColor", MGEffectParameterType.Color);
        AssertParameterType(Parameters, "ButtonRole", MGEffectParameterType.Float);
        AssertParameterType(Parameters, "TreatmentDirection", MGEffectParameterType.Vector2);
        AssertParameterType(Parameters, "TreatmentStrength", MGEffectParameterType.Float);
    }

    private static void AssertParameter(
        IReadOnlyList<XamlEffectParameter> Parameters,
        string Name,
        MGEffectParameterType Type,
        string Value)
    {
        XamlEffectParameter Parameter = Assert.Single(Parameters, x => x.Name == Name);
        Assert.Equal(Type, Parameter.Type);
        Assert.Equal(Value, Parameter.Value);
    }

    private static void AssertParameterType(
        IReadOnlyList<XamlEffectParameter> Parameters,
        string Name,
        MGEffectParameterType Type)
    {
        XamlEffectParameter Parameter = Assert.Single(Parameters, x => x.Name == Name);
        Assert.Equal(Type, Parameter.Type);
    }

    private static int CountOccurrences(string source, string value)
    {
        int Count = 0;
        int StartIndex = 0;
        while ((StartIndex = source.IndexOf(value, StartIndex, StringComparison.Ordinal)) >= 0)
        {
            Count++;
            StartIndex += value.Length;
        }

        return Count;
    }

    private static Vector2 BilinearInterpolate(
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomLeft,
        Vector2 bottomRight,
        Vector2 weights)
    {
        Vector2 top = Vector2.Lerp(topLeft, topRight, weights.X);
        Vector2 bottom = Vector2.Lerp(bottomLeft, bottomRight, weights.X);
        return Vector2.Lerp(top, bottom, weights.Y);
    }
}
