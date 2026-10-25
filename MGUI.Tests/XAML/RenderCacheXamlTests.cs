using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Portable.Xaml;
using Portable.Xaml.Markup;

namespace MGUI.Tests.XAML;

public sealed class RenderCacheXamlTests
{
    private const string Namespace = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    [Theory]
    [InlineData("<TextBlock Text=\"Static\" />")]
    [InlineData("<RenderCache.Content><TextBlock Text=\"Static\" /></RenderCache.Content>")]
    public void ContentSyntaxUsesSingleContentHostAndLeavesRuntimeDefaults(string content)
    {
        RenderCache source = Assert.IsType<RenderCache>(XamlServices.Parse($"<RenderCache xmlns=\"{Namespace}\">{content}</RenderCache>"));
        Assert.IsAssignableFrom<SingleContentHost>(source);
        Assert.Equal(MGElementType.RenderCache, source.ElementType);
        Assert.Null(source.IsCacheEnabled);
        Assert.Null(source.IsRenderVolatile);
        Assert.Equal("Content", typeof(RenderCache).GetCustomAttribute<ContentPropertyAttribute>()!.Name);
        Assert.Equal("Static", Assert.IsType<TextBlock>(source.Content).Text);
    }

    [Fact]
    public void EmptyContentIsValidAndFlagsAreNullableOnOrdinaryElements()
    {
        RenderCache source = Assert.IsType<RenderCache>(XamlServices.Parse($"<RenderCache xmlns=\"{Namespace}\" IsCacheEnabled=\"False\" IsRenderVolatile=\"True\" />"));
        Assert.Null(source.Content);
        Assert.False(source.IsCacheEnabled);
        Assert.True(source.IsRenderVolatile);
        TextBlock ordinary = Assert.IsType<TextBlock>(XamlServices.Parse($"<TextBlock xmlns=\"{Namespace}\" IsRenderVolatile=\"False\" />"));
        Assert.False(ordinary.IsRenderVolatile);
    }

    [Theory]
    [InlineData("IsCacheEnabled=\"sometimes\"", "")]
    [InlineData("IsRenderVolatile=\"sometimes\"", "")]
    [InlineData("", "<RenderCache.Content><SolidFillBrush Color=\"Red\" /></RenderCache.Content>")]
    [InlineData("", "<TextBlock /><TextBlock />")]
    public void InvalidFlagsAndNonElementOrMultipleContentFail(string attributes, string content)
    {
        Assert.ThrowsAny<Exception>(() => XamlServices.Parse($"<RenderCache xmlns=\"{Namespace}\" {attributes}>{content}</RenderCache>"));
    }

    [Fact]
    public void StylesInheritFlagsWithoutOverwritingExplicitFalse()
    {
        StackPanel root = Assert.IsType<StackPanel>(XamlServices.Parse($$"""
            <StackPanel xmlns="{{Namespace}}">
              <StackPanel.Resources><ResourceDictionary>
                <Style Name="Base" TargetType="RenderCache"><Setter Property="IsCacheEnabled" Value="False" /></Style>
                <Style Name="Derived" TargetType="RenderCache" BasedOn="Base"><Setter Property="IsRenderVolatile" Value="True" /></Style>
                <Style TargetType="TextBlock"><Setter Property="IsRenderVolatile" Value="True" /></Style>
              </ResourceDictionary></StackPanel.Resources>
              <RenderCache StyleNames="Derived"><TextBlock /></RenderCache>
              <RenderCache StyleNames="Derived" IsCacheEnabled="True" IsRenderVolatile="False"><TextBlock IsRenderVolatile="False" /></RenderCache>
            </StackPanel>
            """));
        typeof(Element).GetMethod("ProcessStyles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MGResources) }, null)!
            .Invoke(root, new object[] { new MGResources(new MGTheme("TestFont")) });
        RenderCache inherited = Assert.IsType<RenderCache>(root.Children[0]);
        Assert.False(inherited.IsCacheEnabled);
        Assert.True(inherited.IsRenderVolatile);
        Assert.True(Assert.IsType<TextBlock>(inherited.Content).IsRenderVolatile);
        RenderCache explicitValues = Assert.IsType<RenderCache>(root.Children[1]);
        Assert.True(explicitValues.IsCacheEnabled);
        Assert.False(explicitValues.IsRenderVolatile);
        Assert.False(Assert.IsType<TextBlock>(explicitValues.Content).IsRenderVolatile);
    }
}
