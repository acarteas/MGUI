using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;
using XamlEffectParameter = MGUI.Core.UI.XAML.EffectParameter;
using XamlImage = MGUI.Core.UI.XAML.Image;

namespace MGUI.Tests.XAML;

public class ImageEffectXamlTests
{
    [Fact]
    public void Image_ParsesEffectConfigurationAndNestedParameters()
    {
        string xaml = """
            <Image xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                   SourceName="ActionIcon"
                   EffectName="IconGlow"
                   UseStandardParameters="True">
                <EffectParameter Name="GlowColor" Type="Color" Value="rgb(210,165,72)" />
                <EffectParameter Name="GlowStrength" Type="Float" Value="1.25" />
            </Image>
            """;

        XamlImage image = Assert.IsType<XamlImage>(XamlServices.Parse(xaml));

        Assert.Equal("ActionIcon", image.SourceName);
        Assert.Equal("IconGlow", image.EffectName);
        Assert.True(image.UseStandardParameters);
        Assert.Equal(2, image.Parameters.Count);
    }

    [Fact]
    public void ApplyDerivedSettings_ResolvesRegisteredEffectAndConvertsParameters()
    {
        Effect effect = CreateEffect();
        MGResources resources = CreateResources();
        resources.AddEffect("IconGlow", effect);
        XamlImage source = new()
        {
            SourceName = "ActionIcon",
            EffectName = "IconGlow",
            UseStandardParameters = true,
            Parameters =
            {
                new XamlEffectParameter { Name = "GlowColor", Value = "rgb(210,165,72)" },
                new XamlEffectParameter { Name = "GlowStrength", Value = "1.25" }
            }
        };
        MGImage image = CreateRuntimeImage(resources);

        source.ApplyDerivedSettings(null, image, true);

        Assert.Same(effect, image.Effect);
        Assert.True(image.UseStandardParameters);
        Assert.Equal(MGEffectParameterType.Color, image.Parameters[0].Type);
        Assert.IsType<Vector4>(image.Parameters[0].Value);
        Assert.Equal(MGEffectParameterType.Float, image.Parameters[1].Type);
        Assert.Equal(1.25f, image.Parameters[1].Value);
    }

    [Fact]
    public void ApplyDerivedSettings_LeavesImageUnconfiguredWhenEffectSettingsAreOmitted()
    {
        XamlImage source = new() { SourceName = "ActionIcon" };
        MGImage image = CreateRuntimeImage(CreateResources());

        source.ApplyDerivedSettings(null, image, true);

        Assert.Null(image.Effect);
        Assert.False(image.UseStandardParameters);
        Assert.Empty(image.Parameters);
    }

    [Fact]
    public void ApplyDerivedSettings_RejectsEffectSettingsWithoutEffectName()
    {
        XamlImage source = new()
        {
            SourceName = "ActionIcon",
            UseStandardParameters = true,
            Parameters = { new XamlEffectParameter { Name = "GlowStrength", Value = "1" } }
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => source.ApplyDerivedSettings(null, CreateRuntimeImage(CreateResources()), true));

        Assert.Contains(nameof(XamlImage.EffectName), exception.Message);
    }

    [Fact]
    public void ApplyDerivedSettings_RejectsMissingAndBlankEffectNames()
    {
        XamlImage missing = new() { SourceName = "ActionIcon", EffectName = "Missing" };
        InvalidOperationException missingException = Assert.Throws<InvalidOperationException>(
            () => missing.ApplyDerivedSettings(null, CreateRuntimeImage(CreateResources()), true));

        Assert.Contains("Missing", missingException.Message);
        Assert.Contains(nameof(MGResources.AddEffect), missingException.Message);

        XamlImage blank = new() { SourceName = "ActionIcon", EffectName = " " };
        Assert.Throws<ArgumentException>(
            () => blank.ApplyDerivedSettings(null, CreateRuntimeImage(CreateResources()), true));
    }

    [Fact]
    public void ApplyDerivedSettings_ValidatesParametersBeforeEffectLookup()
    {
        XamlImage source = new()
        {
            SourceName = "ActionIcon",
            EffectName = "Missing",
            Parameters = { new XamlEffectParameter { Name = "GlowStrength", Type = MGEffectParameterType.Float, Value = "invalid" } }
        };

        FormatException exception = Assert.Throws<FormatException>(
            () => source.ApplyDerivedSettings(null, CreateRuntimeImage(CreateResources()), true));

        Assert.Contains("GlowStrength", exception.Message);
    }

    [Fact]
    public void ApplyDerivedSettings_UsesEffectRegistrationSnapshot()
    {
        Effect first = CreateEffect();
        Effect replacement = CreateEffect();
        MGResources resources = CreateResources();
        resources.AddEffect("IconGlow", first);
        XamlImage source = new() { SourceName = "ActionIcon", EffectName = "IconGlow" };
        MGImage firstImage = CreateRuntimeImage(resources);

        source.ApplyDerivedSettings(null, firstImage, true);
        resources.AddOrReplaceEffect("IconGlow", replacement);
        MGImage replacementImage = CreateRuntimeImage(resources);
        source.ApplyDerivedSettings(null, replacementImage, true);

        Assert.Same(first, firstImage.Effect);
        Assert.Same(replacement, replacementImage.Effect);
    }

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static MGImage CreateRuntimeImage(MGResources resources)
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        SetField(desktop, "<Resources>k__BackingField", resources);

        MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        SetField(window, "<Desktop>k__BackingField", desktop);

        MGImage image = (MGImage)RuntimeHelpers.GetUninitializedObject(typeof(MGImage));
        SetField(image, "<ParentWindow>k__BackingField", window);
        SetField(image, "Binding", new MGEffectBinding(null));
        return image;
    }

    private static Effect CreateEffect()
        => (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));

    private static void SetField(object target, string fieldName, object value)
    {
        Type? type = target.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        if (field == null)
        {
            throw new InvalidOperationException($"Could not find field '{fieldName}' on {target.GetType().Name}.");
        }

        field.SetValue(target, value);
    }
}
