using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Portable.Xaml;

namespace MGUI.Tests.XAML;

public class ConventionalButtonXamlTests
{
    private static readonly MethodInfo ProcessStylesMethod = typeof(Element)
        .GetMethod("ProcessStyles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MGResources) }, null)!;

    [Fact]
    public void Image_ParsesAllVisualStateTintProperties()
    {
        string xaml = """
            <Image xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                   TextureColor="White" HoveredTextureColor="Green" PressedTextureColor="Red"
                   SelectedTextureColor="Blue" DisabledTextureColor="Gray" />
            """;

        Image image = Assert.IsType<Image>(XamlServices.Parse(xaml));

        Assert.Equal(Color.White, image.TextureColor!.Value.ToXNAColor());
        Assert.Equal(Color.Green, image.HoveredTextureColor!.Value.ToXNAColor());
        Assert.Equal(Color.Red, image.PressedTextureColor!.Value.ToXNAColor());
        Assert.Equal(Color.Blue, image.SelectedTextureColor!.Value.ToXNAColor());
        Assert.Equal(Color.Gray, image.DisabledTextureColor!.Value.ToXNAColor());
    }

    [Fact]
    public void ImageTintAndButtonOffset_CanBeAppliedByStyles()
    {
        MGResources resources = new(new MGTheme("TestFont"));
        resources.AddStyle(new Style
        {
            Name = "TintedImage",
            TargetType = MGElementType.Image,
            Setters = { new Setter { Property = nameof(Image.HoveredTextureColor), Value = "Green" } }
        });
        resources.AddStyle(new Style
        {
            Name = "PressedButton",
            TargetType = MGElementType.Button,
            Setters = { new Setter { Property = nameof(Button.PressedContentOffset), Value = "1,2" } }
        });
        Image image = new() { StyleNames = "TintedImage" };
        Button button = new() { StyleNames = "PressedButton" };

        ProcessStylesMethod.Invoke(image, new object[] { resources });
        ProcessStylesMethod.Invoke(button, new object[] { resources });

        Assert.Equal(Color.Green, image.HoveredTextureColor!.Value.ToXNAColor());
        Assert.Equal(new XAMLPoint(1, 2), button.PressedContentOffset);
    }

    [Fact]
    public void RenderScale_ShorthandAndOverridesResolveIndependently()
    {
        Element shorthand = new Border { RenderScale = 1.1f };
        Element overridden = new Border { RenderScale = 1.1f, HoveredRenderScale = 1.2f, PressedRenderScale = 0.9f };

        ConditionalScaleTransform shorthandScale = shorthand.GetRenderScale()!.Value;
        ConditionalScaleTransform overriddenScale = overridden.GetRenderScale()!.Value;

        Assert.Equal(1.1f, shorthandScale.HoveredScale);
        Assert.Equal(1.1f, shorthandScale.PressedScale);
        Assert.Equal(1.2f, overriddenScale.HoveredScale);
        Assert.Equal(0.9f, overriddenScale.PressedScale);
        Assert.Null(new Border().GetRenderScale());
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RenderScale_InvalidValuesFailWithPropertyDiagnostic(float value)
    {
        Border border = new() { HoveredRenderScale = value };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => border.GetRenderScale());

        Assert.Contains(nameof(Element.HoveredRenderScale), exception.Message);
    }

    [Fact]
    public void Button_ParsesPressedContentOffsetAndSeparateScales()
    {
        string xaml = """
            <Button xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                    PressedContentOffset="1,2" HoveredRenderScale="1.05" PressedRenderScale="0.97" />
            """;

        Button button = Assert.IsType<Button>(XamlServices.Parse(xaml));

        Assert.Equal(new XAMLPoint(1, 2), button.PressedContentOffset);
        Assert.Equal(1.05f, button.HoveredRenderScale);
        Assert.Equal(0.97f, button.PressedRenderScale);
    }
}
