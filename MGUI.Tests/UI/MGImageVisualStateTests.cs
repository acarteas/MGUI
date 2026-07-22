using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Tests.UI;

public class MGImageVisualStateTests
{
    [Theory]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.None, "Normal")]
    [InlineData(PrimaryVisualState.Selected, SecondaryVisualState.None, "Selected")]
    [InlineData(PrimaryVisualState.Normal, SecondaryVisualState.Hovered, "Hovered")]
    [InlineData(PrimaryVisualState.Selected, SecondaryVisualState.Pressed, "Pressed")]
    [InlineData(PrimaryVisualState.Disabled, SecondaryVisualState.Pressed, "Disabled")]
    public void GetTextureColor_UsesDocumentedPrecedence(PrimaryVisualState Primary, SecondaryVisualState Secondary, string Expected)
    {
        MGImage image = CreateImage();
        image.TextureColor = Color.White;
        image.SelectedTextureColor = Color.Blue;
        image.HoveredTextureColor = Color.Green;
        image.PressedTextureColor = Color.Red;
        image.DisabledTextureColor = Color.Gray;

        Color result = image.GetTextureColor(new VisualState(Primary, Secondary));

        Assert.Equal(Expected switch
        {
            "Selected" => Color.Blue,
            "Hovered" => Color.Green,
            "Pressed" => Color.Red,
            "Disabled" => Color.Gray,
            _ => Color.White
        }, result);
    }

    [Fact]
    public void GetTextureColor_MissingStateValueFallsDirectlyToBaseThenWhite()
    {
        MGImage image = CreateImage();
        image.SelectedTextureColor = Color.Blue;
        image.TextureColor = Color.Goldenrod;

        Assert.Equal(Color.Goldenrod, image.GetTextureColor(new VisualState(PrimaryVisualState.Selected, SecondaryVisualState.Hovered)));
        image.TextureColor = null;
        Assert.Equal(Color.White, image.GetTextureColor(new VisualState(PrimaryVisualState.Selected, SecondaryVisualState.Hovered)));
    }

    [Fact]
    public void GetDrawColor_MultipliesDrawAndTextureOpacityOnce()
    {
        MGImage image = CreateImage();
        image.TextureColor = new Color(200, 100, 50, 255);

        Color result = image.GetDrawColor(default, 0.5f, 0.5f);

        Assert.Equal(image.TextureColor.Value * 0.25f, result);
    }

    [Fact]
    public void TintPropertyChangesNotifyWithoutLayoutChange()
    {
        MGImage image = CreateImage();
        List<string> changed = new();
        ((INotifyPropertyChanged)image).PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        image.HoveredTextureColor = Color.Green;
        image.PressedTextureColor = Color.Red;
        image.SelectedTextureColor = Color.Blue;
        image.DisabledTextureColor = Color.Gray;

        Assert.Equal(new[]
        {
            nameof(MGImage.HoveredTextureColor), nameof(MGImage.PressedTextureColor),
            nameof(MGImage.SelectedTextureColor), nameof(MGImage.DisabledTextureColor)
        }, changed);
    }

    [Fact]
    public void SamplerTypeChangesNotifyWithoutLayoutChange()
    {
        MGImage image = CreateImage();
        List<string> changed = new();
        ((INotifyPropertyChanged)image).PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        image.SamplerType = SamplerType.LinearClamp;

        Assert.Equal(new[] { nameof(MGImage.SamplerType) }, changed);
    }

    [Fact]
    public void EffectSettingsNotifyWithoutLayoutChangeAndSnapshotParameters()
    {
        MGImage image = CreateImage();
        Effect effect = (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        List<string> changed = new();
        ((INotifyPropertyChanged)image).PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        MGEffectParameterValue[] parameters = { new("Strength", MGEffectParameterType.Float, 0.5f) };

        image.Effect = effect;
        image.UseStandardParameters = true;
        image.Parameters = parameters;
        parameters[0] = new MGEffectParameterValue("Strength", MGEffectParameterType.Float, 1.0f);

        Assert.Equal(new[]
        {
            nameof(MGImage.Effect),
            nameof(MGImage.UseStandardParameters),
            nameof(MGImage.Parameters)
        }, changed);
        Assert.Same(effect, image.Effect);
        Assert.Equal(0.5f, image.Parameters[0].Value);
    }

    private static MGImage CreateImage()
    {
        MGImage image = (MGImage)RuntimeHelpers.GetUninitializedObject(typeof(MGImage));
        FieldInfo bindingField = typeof(MGImage).GetField("Binding", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find the image effect binding.");
        bindingField.SetValue(image, new MGEffectBinding(null));
        return image;
    }
}
