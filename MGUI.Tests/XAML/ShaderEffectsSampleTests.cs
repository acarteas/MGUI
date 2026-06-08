using System.Reflection;
using Portable.Xaml;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.XAML;

public class ShaderEffectsSampleTests
{
    [Fact]
    public void SampleXamlParsesAndDemonstratesRequiredFeatures()
    {
        string Source = ReadResource("ShaderEffects.xaml");

        Assert.IsType<Window>(XamlServices.Parse(Source));
        Assert.Contains("UseStandardParameters=\"True\"", Source);
        Assert.Contains("<EffectParameter Name=\"AccentColor\" Type=\"Color\"", Source);
        Assert.Contains("<EffectParameter Name=\"ButtonRole\" Type=\"Float\"", Source);
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
    public void SampleRegistersResourcesBeforeMaterializationWithoutElementLookups()
    {
        string Source = ReadResource("ShaderEffects.xaml.cs");

        Assert.Contains("\"ShaderEffects.xaml\", () => RegisterUiEffect(Content, Desktop.Resources)", Source);
        Assert.Contains("AddOrReplaceEffect", Source);
        Assert.DoesNotContain("GetElementByName", Source);
    }

    [Fact]
    public void SampleShaderAppliesOpacityOnce()
    {
        string Source = ReadResource("UiEffects.fx");

        Assert.Contains("input.Color * result", Source);
        Assert.DoesNotContain("result.a *= Opacity", Source);
        Assert.Contains("HoverAmount", Source);
        Assert.Contains("PressAmount", Source);
        Assert.Contains("SelectedAmount", Source);
        Assert.Contains("DisabledAmount", Source);
    }

    private static string ReadResource(string Suffix)
    {
        Assembly Assembly = typeof(ShaderEffectsSampleTests).Assembly;
        string Name = Assert.Single(Assembly.GetManifestResourceNames().Where(x => x.EndsWith(Suffix, StringComparison.Ordinal)));
        using Stream Stream = Assert.IsAssignableFrom<Stream>(Assembly.GetManifestResourceStream(Name));
        using StreamReader Reader = new(Stream);
        return Reader.ReadToEnd();
    }
}
