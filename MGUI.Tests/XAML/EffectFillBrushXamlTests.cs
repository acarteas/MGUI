using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;
using System.Globalization;
using XamlEffectParameter = MGUI.Core.UI.XAML.EffectParameter;
using XamlRectangle = MGUI.Core.UI.XAML.Rectangle;

namespace MGUI.Tests.XAML;

public class EffectFillBrushXamlTests
{
    [Fact]
    public void EffectFillBrush_ParsesFallbackAndCustomParameters()
    {
        string xaml = """
            <EffectFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                             EffectName="HudButtonEffect">
                <EffectFillBrush.FallbackBrush>
                    <SolidFillBrush Color="rgb(46,41,40)" />
                </EffectFillBrush.FallbackBrush>
                <EffectParameter Name="AccentColor" Value="rgb(210,165,72)" />
                <EffectParameter Name="ButtonRole" Value="1" />
                <EffectParameter Name="Direction" Type="Vector2" Value="1, 0" />
            </EffectFillBrush>
            """;

        EffectFillBrush brush = Assert.IsType<EffectFillBrush>(XamlServices.Parse(xaml));

        Assert.Equal("HudButtonEffect", brush.EffectName);
        Assert.False(brush.UseStandardParameters);
        Assert.IsType<SolidFillBrush>(brush.FallbackBrush);
        Assert.Equal(3, brush.Parameters.Count);
    }

    [Fact]
    public void ToFillBrush_ResolvesRegisteredEffectAndConvertsParameters()
    {
        Effect effect = CreateEffect();
        MGResources resources = CreateResources();
        resources.AddEffect("Hud", effect);
        MGDesktop desktop = CreateDesktop(resources);
        EffectFillBrush source = new()
        {
            EffectName = "Hud",
            Parameters =
            {
                new XamlEffectParameter { Name = "Accent", Value = "rgb(210,165,72)" },
                new XamlEffectParameter { Name = "Role", Value = "1" },
                new XamlEffectParameter { Name = "Enabled", Value = "true" }
            }
        };

        MGEffectFillBrush brush = Assert.IsType<MGEffectFillBrush>(source.ToFillBrush(desktop, null));

        Assert.Same(effect, brush.Effect);
        Assert.False(brush.UseStandardParameters);
        Assert.Equal(MGEffectParameterType.Color, brush.Parameters[0].Type);
        Assert.IsType<Vector4>(brush.Parameters[0].Value);
        Assert.Equal(MGEffectParameterType.Int, brush.Parameters[1].Type);
        Assert.Equal(MGEffectParameterType.Bool, brush.Parameters[2].Type);
    }

    [Fact]
    public void ToFillBrush_UsesFallbackWhenEffectIsMissing()
    {
        MGDesktop desktop = CreateDesktop(CreateResources());
        EffectFillBrush source = new()
        {
            EffectName = "Missing",
            FallbackBrush = new SolidFillBrush(new XAMLColor(Color.CornflowerBlue))
        };

        IFillBrush brush = source.ToFillBrush(desktop, null);

        Assert.IsType<MGSolidFillBrush>(brush);
    }

    [Fact]
    public void ToFillBrush_ThrowsActionableErrorWhenEffectAndFallbackAreMissing()
    {
        MGDesktop desktop = CreateDesktop(CreateResources());
        EffectFillBrush source = new() { EffectName = "Missing" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => source.ToFillBrush(desktop, null));

        Assert.Contains("AddEffect", exception.Message);
        Assert.Contains("before XAML brush materialization", exception.Message);
        Assert.Contains("FallbackBrush", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToFillBrush_RejectsInvalidEffectNameWithoutUsingFallback(string? effectName)
    {
        EffectFillBrush source = new()
        {
            EffectName = effectName,
            FallbackBrush = new SolidFillBrush(new XAMLColor(Color.Red))
        };

        Assert.Throws<ArgumentException>(() => source.ToFillBrush(CreateDesktop(CreateResources()), null));
    }

    [Fact]
    public void EffectFillBrush_ParsesInRepresentativeElementBrushSlots()
    {
        string xaml = """
            <Border xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <Border.Background><EffectFillBrush EffectName="Normal" /></Border.Background>
                <Border.DisabledBackground><EffectFillBrush EffectName="Disabled" /></Border.DisabledBackground>
                <Border.SelectedBackground><EffectFillBrush EffectName="Selected" /></Border.SelectedBackground>
                <Border.Overlay><EffectFillBrush EffectName="Overlay" /></Border.Overlay>
            </Border>
            """;

        Border border = Assert.IsType<Border>(XamlServices.Parse(xaml));

        Assert.Equal("Normal", Assert.IsType<EffectFillBrush>(border.Background).EffectName);
        Assert.Equal("Disabled", Assert.IsType<EffectFillBrush>(border.DisabledBackground).EffectName);
        Assert.Equal("Selected", Assert.IsType<EffectFillBrush>(border.SelectedBackground).EffectName);
        Assert.Equal("Overlay", Assert.IsType<EffectFillBrush>(border.Overlay).EffectName);
    }

    [Fact]
    public void EffectFillBrush_ParsesInControlSpecificAndNestedBrushSlots()
    {
        string rectangleXaml = """
            <Rectangle xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <Rectangle.Fill><EffectFillBrush EffectName="Rectangle" /></Rectangle.Fill>
            </Rectangle>
            """;
        string paddedXaml = """
            <PaddedFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <PaddedFillBrush.Brush><EffectFillBrush EffectName="Nested" /></PaddedFillBrush.Brush>
            </PaddedFillBrush>
            """;

        XamlRectangle rectangle = Assert.IsType<XamlRectangle>(XamlServices.Parse(rectangleXaml));
        PaddedFillBrush padded = Assert.IsType<PaddedFillBrush>(XamlServices.Parse(paddedXaml));

        Assert.Equal("Rectangle", Assert.IsType<EffectFillBrush>(rectangle.Fill).EffectName);
        Assert.Equal("Nested", Assert.IsType<EffectFillBrush>(padded.Brush).EffectName);
    }

    [Fact]
    public void GetNestedBindableObjects_IncludesFallbackBrush()
    {
        SolidFillBrush fallback = new(new XAMLColor(Color.Red));
        EffectFillBrush source = new() { EffectName = "Hud", FallbackBrush = fallback };

        var nested = source.GetNestedBindableObjects().ToArray();

        Assert.Contains(nested, x => ReferenceEquals(x.Item1, fallback) && x.Item2 == nameof(EffectFillBrush.FallbackBrush));
    }

    [Fact]
    public void Materialization_UsesSnapshotAcrossReplacementAndRemoval()
    {
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        MGResources resources = CreateResources();
        resources.AddEffect("Hud", first);
        MGDesktop desktop = CreateDesktop(resources);
        EffectFillBrush source = new()
        {
            EffectName = "Hud",
            FallbackBrush = new SolidFillBrush(new XAMLColor(Color.Red))
        };

        MGEffectFillBrush firstBrush = Assert.IsType<MGEffectFillBrush>(source.ToFillBrush(desktop, null));
        resources.AddOrReplaceEffect("Hud", replacement);
        MGEffectFillBrush replacementBrush = Assert.IsType<MGEffectFillBrush>(source.ToFillBrush(desktop, null));
        resources.RemoveEffect("Hud");
        IFillBrush fallbackBrush = source.ToFillBrush(desktop, null);

        Assert.Same(first, firstBrush.Effect);
        Assert.Same(replacement, replacementBrush.Effect);
        Assert.IsType<MGSolidFillBrush>(fallbackBrush);
    }

    [Fact]
    public void ToFillBrush_ConvertsEverySupportedCustomTypeCultureInvariantly()
    {
        Effect effect = CreateEffect();
        MGResources resources = CreateResources();
        resources.AddEffect("Hud", effect);
        EffectFillBrush source = new()
        {
            EffectName = "Hud",
            UseStandardParameters = true,
            Parameters =
            {
                new() { Name = "F", Type = MGEffectParameterType.Float, Value = "1.25" },
                new() { Name = "I", Type = MGEffectParameterType.Int, Value = "2" },
                new() { Name = "B", Type = MGEffectParameterType.Bool, Value = "TRUE" },
                new() { Name = "C", Type = MGEffectParameterType.Color, Value = "rgb(210,165,72)" },
                new() { Name = "V2", Type = MGEffectParameterType.Vector2, Value = "1, 2" },
                new() { Name = "V3", Type = MGEffectParameterType.Vector3, Value = "3, 4, 5" },
                new() { Name = "V4", Type = MGEffectParameterType.Vector4, Value = "6, 7, 8, 9" }
            }
        };
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            MGEffectFillBrush brush = Assert.IsType<MGEffectFillBrush>(source.ToFillBrush(CreateDesktop(resources), null));

            Assert.True(brush.UseStandardParameters);
            Assert.Equal(1.25f, brush.Parameters[0].Value);
            Assert.Equal(2, brush.Parameters[1].Value);
            Assert.Equal(true, brush.Parameters[2].Value);
            Assert.Equal(new XAMLColor(210, 165, 72).ToXNAColor().ToVector4(), brush.Parameters[3].Value);
            Assert.Equal(new Vector2(1, 2), brush.Parameters[4].Value);
            Assert.Equal(new Vector3(3, 4, 5), brush.Parameters[5].Value);
            Assert.Equal(new Vector4(6, 7, 8, 9), brush.Parameters[6].Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("true", MGEffectParameterType.Bool)]
    [InlineData("1", MGEffectParameterType.Int)]
    [InlineData("1.5", MGEffectParameterType.Float)]
    [InlineData("rgb(1,2,3)", MGEffectParameterType.Color)]
    public void EffectParameter_InferenceIsDeterministic(string value, MGEffectParameterType expectedType)
    {
        MGEffectParameterValue result = new XamlEffectParameter { Name = "Value", Value = value }.ToParameterValue();

        Assert.Equal(expectedType, result.Type);
    }

    [Fact]
    public void EffectParameter_VectorInferenceRequiresExplicitType()
    {
        FormatException exception = Assert.Throws<FormatException>(
            () => new XamlEffectParameter { Name = "Direction", Value = "1,2" }.ToParameterValue());

        Assert.Contains("explicit Type", exception.Message);
    }

    [Fact]
    public void ToFillBrush_RejectsDuplicateCustomParameterNames()
    {
        MGResources resources = CreateResources();
        resources.AddEffect("Hud", CreateEffect());
        EffectFillBrush source = new()
        {
            EffectName = "Hud",
            Parameters =
            {
                new() { Name = "Role", Value = "1" },
                new() { Name = "Role", Value = "2" }
            }
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Contains("Duplicate", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Theory]
    [InlineData(null, MGEffectParameterType.Float, "1")]
    [InlineData(" ", MGEffectParameterType.Float, "1")]
    [InlineData("BadFloat", MGEffectParameterType.Float, "abc")]
    [InlineData("BadVector", MGEffectParameterType.Vector3, "1,2")]
    [InlineData("Unsupported", (MGEffectParameterType)999, "1")]
    public void EffectParameter_InvalidDeclarationFailsClearly(string? name, MGEffectParameterType type, string value)
    {
        XamlEffectParameter parameter = new() { Name = name, Type = type, Value = value };

        Exception exception = Assert.ThrowsAny<Exception>(() => parameter.ToParameterValue());

        Assert.Contains(name?.Trim() is { Length: > 0 } ? name : nameof(XamlEffectParameter.Name), exception.Message);
    }

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static MGDesktop CreateDesktop(MGResources resources)
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        typeof(MGDesktop).GetField("<Resources>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(desktop, resources);
        return desktop;
    }

    private static Effect CreateEffect()
    {
        return (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
    }
}
