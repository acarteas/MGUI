using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;
using XamlEffectParameter = MGUI.Core.UI.XAML.EffectParameter;

namespace MGUI.Tests.XAML;

public class NineSliceFillBrushXamlTests
{
    [Fact]
    public void NineSliceFillBrush_ParsesInteriorBrushAttribute()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="PanelFrame"
                                SourceMargin="64"
                                TargetMargin="32"
                                InteriorBrush="Red" />
            """;

        NineSliceFillBrush brush = (NineSliceFillBrush)XamlServices.Parse(xaml);

        SolidFillBrush interiorBrush = Assert.IsType<SolidFillBrush>(brush.InteriorBrush);
        Assert.Equal(255, interiorBrush.Color.R);
        Assert.Equal(0, interiorBrush.Color.G);
        Assert.Equal(0, interiorBrush.Color.B);
        Assert.Equal(255, interiorBrush.Color.A);
    }

    [Fact]
    public void NineSliceFillBrush_ParsesInteriorBrushPropertyElement()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="PanelFrame"
                                SourceMargin="64"
                                TargetMargin="32">
                <NineSliceFillBrush.InteriorBrush>
                    <TextureFillBrush SourceName="PanelInterior" Stretch="Fill" />
                </NineSliceFillBrush.InteriorBrush>
            </NineSliceFillBrush>
            """;

        NineSliceFillBrush brush = (NineSliceFillBrush)XamlServices.Parse(xaml);

        TextureFillBrush interiorBrush = Assert.IsType<TextureFillBrush>(brush.InteriorBrush);
        Assert.Equal("PanelInterior", interiorBrush.SourceName);
        Assert.Equal(Stretch.Fill, interiorBrush.Stretch);
    }

    [Fact]
    public void NineSliceFillBrush_ParsesRepresentativeEffectConfiguration()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="PanelFrame"
                                SourceMargin="64"
                                TargetMargin="32"
                                EffectName="PanelEffect"
                                UseStandardParameters="True">
                <NineSliceFillBrush.InteriorBrush>
                    <SolidFillBrush Color="rgb(46,41,40)" />
                </NineSliceFillBrush.InteriorBrush>
                <EffectParameter Name="AccentColor" Value="rgb(210,165,72)" />
                <EffectParameter Name="ButtonRole" Value="1" />
                <EffectParameter Name="Direction" Type="Vector2" Value="1, 0" />
            </NineSliceFillBrush>
            """;

        NineSliceFillBrush brush = Assert.IsType<NineSliceFillBrush>(XamlServices.Parse(xaml));

        Assert.Equal("PanelFrame", brush.SourceName);
        Assert.Equal("PanelEffect", brush.EffectName);
        Assert.True(brush.UseStandardParameters);
        Assert.IsType<SolidFillBrush>(brush.InteriorBrush);
        Assert.Equal(3, brush.Parameters.Count);
    }

    [Fact]
    public void ToFillBrush_ResolvesEffectAndMaterializesInteriorBrush()
    {
        Effect effect = CreateEffect();
        MGResources resources = CreateResources();
        AddTexture(resources);
        resources.AddEffect("PanelEffect", effect);
        NineSliceFillBrush source = CreateEffectSource();
        source.InteriorBrush = new SolidFillBrush(new XAMLColor(Color.CornflowerBlue));
        source.Parameters.Add(new XamlEffectParameter { Name = "Role", Value = "2" });

        MGNineSliceFillBrush brush = Assert.IsType<MGNineSliceFillBrush>(
            source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Same(effect, brush.Effect);
        Assert.True(brush.UseStandardParameters);
        Assert.Equal(2, brush.Parameters[0].Value);
        Assert.IsType<MGSolidFillBrush>(brush.InteriorBrush);
    }

    [Fact]
    public void ToFillBrush_PreservesEffectFreeBehaviorWhenEffectNameIsAbsent()
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        NineSliceFillBrush source = new()
        {
            SourceName = "PanelFrame",
            SourceMargin = new MGUI.Core.UI.XAML.Thickness(1),
            TargetMargin = new MGUI.Core.UI.XAML.Thickness(2),
            InteriorBrush = new SolidFillBrush(new XAMLColor(Color.Red))
        };

        MGNineSliceFillBrush brush = Assert.IsType<MGNineSliceFillBrush>(
            source.ToFillBrush(CreateDesktop(resources), null));

        Assert.False(brush.HasEffectBinding);
        Assert.Null(brush.Effect);
        Assert.Empty(brush.Parameters);
        Assert.IsType<MGSolidFillBrush>(brush.InteriorBrush);
    }

    [Fact]
    public void ParsedBrush_RejectsParametersWithoutEffectNameBeforeResourceMaterialization()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="MissingTexture"
                                SourceMargin="1"
                                TargetMargin="2">
                <NineSliceFillBrush.InteriorBrush>
                    <TextureFillBrush SourceName="MissingInterior" />
                </NineSliceFillBrush.InteriorBrush>
                <EffectParameter Name="Role" Value="2" />
            </NineSliceFillBrush>
            """;
        NineSliceFillBrush source = Assert.IsType<NineSliceFillBrush>(XamlServices.Parse(xaml));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(CreateResources()), null));

        Assert.Contains(nameof(NineSliceFillBrush.Parameters), exception.Message);
        Assert.Contains(nameof(NineSliceFillBrush.EffectName), exception.Message);
        Assert.DoesNotContain("MissingTexture", exception.Message);
        Assert.DoesNotContain("MissingInterior", exception.Message);
    }

    [Fact]
    public void ParsedBrush_RejectsStandardParametersWithoutEffectNameBeforeResourceMaterialization()
    {
        string xaml = """
            <NineSliceFillBrush xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                                SourceName="MissingTexture"
                                SourceMargin="1"
                                TargetMargin="2"
                                UseStandardParameters="True">
                <NineSliceFillBrush.InteriorBrush>
                    <TextureFillBrush SourceName="MissingInterior" />
                </NineSliceFillBrush.InteriorBrush>
            </NineSliceFillBrush>
            """;
        NineSliceFillBrush source = Assert.IsType<NineSliceFillBrush>(XamlServices.Parse(xaml));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(CreateResources()), null));

        Assert.Contains(nameof(NineSliceFillBrush.UseStandardParameters), exception.Message);
        Assert.Contains(nameof(NineSliceFillBrush.EffectName), exception.Message);
        Assert.DoesNotContain("MissingTexture", exception.Message);
        Assert.DoesNotContain("MissingInterior", exception.Message);
    }

    [Fact]
    public void ToFillBrush_RejectsProgrammaticParametersWithoutEffectName()
    {
        NineSliceFillBrush source = new()
        {
            Parameters =
            {
                new XamlEffectParameter { Name = "Role", Value = "2" }
            }
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(CreateResources()), null));

        Assert.Contains(nameof(NineSliceFillBrush.Parameters), exception.Message);
        Assert.Contains(nameof(NineSliceFillBrush.EffectName), exception.Message);
    }

    [Fact]
    public void ToFillBrush_RejectsProgrammaticStandardParametersWithoutEffectName()
    {
        NineSliceFillBrush source = new() { UseStandardParameters = true };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(CreateResources()), null));

        Assert.Contains(nameof(NineSliceFillBrush.UseStandardParameters), exception.Message);
        Assert.Contains(nameof(NineSliceFillBrush.EffectName), exception.Message);
    }

    [Fact]
    public void Materialization_UsesEffectSnapshotAcrossReplacementAndRemoval()
    {
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        MGResources resources = CreateResources();
        AddTexture(resources);
        resources.AddEffect("PanelEffect", first);
        MGDesktop desktop = CreateDesktop(resources);
        NineSliceFillBrush source = CreateEffectSource();

        MGNineSliceFillBrush firstBrush = Assert.IsType<MGNineSliceFillBrush>(source.ToFillBrush(desktop, null));
        resources.AddOrReplaceEffect("PanelEffect", replacement);
        MGNineSliceFillBrush replacementBrush = Assert.IsType<MGNineSliceFillBrush>(source.ToFillBrush(desktop, null));
        resources.RemoveEffect("PanelEffect");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(desktop, null));

        Assert.Same(first, firstBrush.Effect);
        Assert.Same(replacement, replacementBrush.Effect);
        Assert.Contains("PanelEffect", exception.Message);
        Assert.Contains(nameof(MGResources.AddEffect), exception.Message);
        Assert.Contains("before XAML brush materialization", exception.Message);
    }

    [Fact]
    public void ToFillBrush_ConvertsEverySupportedParameterTypeCultureInvariantly()
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        resources.AddEffect("PanelEffect", CreateEffect());
        NineSliceFillBrush source = CreateEffectSource();
        source.Parameters.AddRange(new[]
        {
            new XamlEffectParameter { Name = "F", Type = MGEffectParameterType.Float, Value = "1.25" },
            new XamlEffectParameter { Name = "I", Type = MGEffectParameterType.Int, Value = "2" },
            new XamlEffectParameter { Name = "B", Type = MGEffectParameterType.Bool, Value = "TRUE" },
            new XamlEffectParameter { Name = "C", Type = MGEffectParameterType.Color, Value = "rgb(210,165,72)" },
            new XamlEffectParameter { Name = "V2", Type = MGEffectParameterType.Vector2, Value = "1, 2" },
            new XamlEffectParameter { Name = "V3", Type = MGEffectParameterType.Vector3, Value = "3, 4, 5" },
            new XamlEffectParameter { Name = "V4", Type = MGEffectParameterType.Vector4, Value = "6, 7, 8, 9" }
        });
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            MGNineSliceFillBrush brush = Assert.IsType<MGNineSliceFillBrush>(
                source.ToFillBrush(CreateDesktop(resources), null));

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
    [InlineData("")]
    [InlineData("   ")]
    public void ToFillBrush_RejectsBlankConfiguredEffectName(string effectName)
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        NineSliceFillBrush source = CreateEffectSource();
        source.EffectName = effectName;

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Contains(nameof(NineSliceFillBrush.EffectName), exception.Message);
    }

    [Fact]
    public void ToFillBrush_RejectsDuplicateParameterNamesCaseSensitively()
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        resources.AddEffect("PanelEffect", CreateEffect());
        NineSliceFillBrush source = CreateEffectSource();
        source.Parameters.Add(new XamlEffectParameter { Name = "Role", Value = "1" });
        source.Parameters.Add(new XamlEffectParameter { Name = "Role", Value = "2" });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Contains("Duplicate", exception.Message);
        Assert.Contains("Role", exception.Message);
        Assert.Contains(nameof(NineSliceFillBrush), exception.Message);
        Assert.Contains("case-sensitive", exception.Message);
    }

    [Theory]
    [InlineData(null, MGEffectParameterType.Float, "1")]
    [InlineData(" ", MGEffectParameterType.Float, "1")]
    [InlineData("BadFloat", MGEffectParameterType.Float, "abc")]
    [InlineData("BadVector", MGEffectParameterType.Vector3, "1,2")]
    [InlineData("NonFinite", MGEffectParameterType.Vector2, "NaN,1")]
    [InlineData("Unsupported", (MGEffectParameterType)999, "1")]
    public void ToFillBrush_RejectsMalformedParameterDeclarations(
        string? name,
        MGEffectParameterType type,
        string value)
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        resources.AddEffect("PanelEffect", CreateEffect());
        NineSliceFillBrush source = CreateEffectSource();
        source.Parameters.Add(new XamlEffectParameter { Name = name, Type = type, Value = value });

        Exception exception = Assert.ThrowsAny<Exception>(
            () => source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Contains(name?.Trim() is { Length: > 0 } ? name : nameof(XamlEffectParameter.Name), exception.Message);
    }

    [Fact]
    public void ToFillBrush_ValidatesParametersBeforeReportingMissingEffect()
    {
        MGResources resources = CreateResources();
        AddTexture(resources);
        NineSliceFillBrush source = CreateEffectSource();
        source.Parameters.Add(new XamlEffectParameter
        {
            Name = "Direction",
            Type = MGEffectParameterType.Vector2,
            Value = "NaN,1"
        });

        FormatException exception = Assert.Throws<FormatException>(
            () => source.ToFillBrush(CreateDesktop(resources), null));

        Assert.Contains("Direction", exception.Message);
        Assert.Contains("finite", exception.Message);
    }

    private static NineSliceFillBrush CreateEffectSource()
        => new()
        {
            SourceName = "PanelFrame",
            SourceMargin = new MGUI.Core.UI.XAML.Thickness(1),
            TargetMargin = new MGUI.Core.UI.XAML.Thickness(2),
            EffectName = "PanelEffect",
            UseStandardParameters = true
        };

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static void AddTexture(MGResources resources)
    {
        Texture2D texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        resources.AddTexture("PanelFrame", new MGTextureData(texture));
    }

    private static MGDesktop CreateDesktop(MGResources resources)
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        typeof(MGDesktop).GetField("<Resources>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(desktop, resources);
        return desktop;
    }

    private static Effect CreateEffect()
        => (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
}
