using System.Xml.Linq;
using System.Xml.Schema;
using MGUI.Core.UI.XAML;
using Xunit;

public class SchemaValidationTests
{
    private static readonly XmlSchemaSet Schemas = CreateSchemas();

    [Theory]
    [InlineData("ListBox")]
    [InlineData("ComboBox")]
    [InlineData("ListView")]
    public void TypeReferencesAreAccepted(string elementName)
    {
        AssertValid($$"""
            <{{elementName}} xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}"
                           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                           xmlns:sys="clr-namespace:System;assembly=System.Runtime"
                           ItemType="{x:Type sys:String}" />
            """);
    }

    [Theory]
    [InlineData("ListBox", "Items", "{MGBinding Path=Inventory}")]
    [InlineData("ListBox", "Items", "{x:Null}")]
    [InlineData("ComboBox", "Items", "{MGBinding Path=Choices}")]
    [InlineData("ListBox", "AlternatingRowBackgrounds", "{x:Null}")]
    [InlineData("ListBox", "AlternatingRowBackgrounds", "{MGBinding Path=RowBrushes}")]
    public void CollectionMarkupExtensionsAreAccepted(string elementName, string propertyName, string value)
    {
        AssertValid($$"""
            <{{elementName}} xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}"
                           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                           {{propertyName}}="{{value}}" />
            """);
    }

    [Fact]
    public void ListBoxPropertyElementsAreStillAccepted()
    {
        AssertValid($$"""
            <ListBox xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}"
                     xmlns:sys="clr-namespace:System;assembly=System.Runtime">
                <ListBox.Items>
                    <sys:String>One</sys:String>
                    <sys:String>Two</sys:String>
                </ListBox.Items>
                <ListBox.AlternatingRowBackgrounds>
                    <SolidFillBrush Color="Red" />
                    <SolidFillBrush Color="Blue" />
                </ListBox.AlternatingRowBackgrounds>
            </ListBox>
            """);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ResourceDictionary.Entries")]
    [InlineData("ResourceDictionary.Styles")]
    public void ResourceDictionaryStylesAreAccepted(string? wrapperName)
    {
        XElement style = XElement.Parse($$"""
            <Style xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}" Name="PrimaryButton" TargetType="Button">
                <Setter Property="Opacity" Value="0.5" />
            </Style>
            """);
        XElement dictionary = new(XName.Get("ResourceDictionary", XAMLParser.XMLLocalNameSpaceUri));
        dictionary.Add(wrapperName == null
            ? style
            : new XElement(XName.Get(wrapperName, XAMLParser.XMLLocalNameSpaceUri), style));

        AssertValid(dictionary.ToString());
    }

    [Fact]
    public void ResourceDictionaryMixedContentMatchesXamlRuntime()
    {
        string xaml = $$"""
            <ResourceDictionary xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}">
                <ColorResource Key="Accent" Value="Red" />
                <Style Name="PrimaryButton" TargetType="Button">
                    <Setter Property="Opacity" Value="0.5" />
                </Style>
                <ColorResource Key="Warning" Value="Blue" />
            </ResourceDictionary>
            """;

        AssertValid(xaml);
        ResourceDictionary dictionary = XAMLParser.LoadStyleDictionary(xaml);
        Assert.Equal(new[] { "ColorResource", "Style", "ColorResource" },
            dictionary.Entries.Select(x => x.GetType().Name));
        Assert.Equal("PrimaryButton", Assert.Single(dictionary.Styles).Name);
        Assert.Equal(2, dictionary.ColorResources.Count);
    }

    [Theory]
    [InlineData("ListBox")]
    [InlineData("ComboBox")]
    public void ObjectCollectionContentAcceptsDirectSchemaElements(string elementName)
    {
        AssertValid($$"""
            <{{elementName}} xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}">
                <TextBlock Text="One" />
                <TextBlock Text="Two" />
            </{{elementName}}>
            """);
    }

    [Fact]
    public void ObjectCollectionContentRejectsUnknownSchemaElements()
    {
        Assert.NotEmpty(Validate($$"""
            <ResourceDictionary xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}">
                <UnknownElement />
            </ResourceDictionary>
            """));
    }

    [Fact]
    public void XamlRuntimeAcceptsTypeAndNullCollectionAttributes()
    {
        ListBox listBox = (ListBox)Portable.Xaml.XamlServices.Parse($$"""
            <ListBox xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     xmlns:sys="clr-namespace:System;assembly=System.Runtime"
                     ItemType="{x:Type sys:String}" Items="{x:Null}"
                     AlternatingRowBackgrounds="{x:Null}" />
            """);

        Assert.Equal(typeof(string), listBox.ItemType);
        Assert.Null(listBox.Items);
        Assert.Null(listBox.AlternatingRowBackgrounds);
    }

    [Theory]
    [InlineData("UnknownProperty", "value")]
    [InlineData("Items", "One,Two")]
    [InlineData("AlternatingRowBackgrounds", "Red,Blue")]
    [InlineData("Width", "invalid")]
    [InlineData("IsTitleVisible", "invalid")]
    [InlineData("SelectionMode", "invalid")]
    public void InvalidAttributesAreStillRejected(string propertyName, string value)
    {
        Assert.NotEmpty(Validate($$"""
            <ListBox xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}" {{propertyName}}="{{value}}" />
            """));
    }

    [Fact]
    public void CacheXamlDeclarationsAreAccepted()
    {
        AssertValid($$"""
            <StackPanel xmlns="{{XAMLParser.XMLLocalNameSpaceUri}}">
                <RenderCache />
                <RenderCache IsCacheEnabled="True">
                    <RenderCache.Content>
                        <TextBlock Text="Static" IsRenderVolatile="False">
                            <TextBlock.Resources>
                                <ResourceDictionary>
                                    <ResourceDictionary.ColorResources>
                                        <ColorResource Key="CacheAccent" Value="Red" />
                                    </ResourceDictionary.ColorResources>
                                </ResourceDictionary>
                            </TextBlock.Resources>
                            <TextBlock.Background>
                                <CachedFillBrush IsCacheEnabled="True" SourceMode="ExplicitlyStatic">
                                    <SolidFillBrush Color="{StaticResource CacheAccent}" />
                                </CachedFillBrush>
                            </TextBlock.Background>
                        </TextBlock>
                    </RenderCache.Content>
                </RenderCache>
                <RenderCache IsCacheEnabled="{MGBinding Path=CacheEnabled}">
                    <TextBlock Text="Bound cache" IsRenderVolatile="{MGBinding Path=IsLive}" />
                </RenderCache>
                <TextBlock Text="Live sibling" IsRenderVolatile="True" />
                <RenderCache IsCacheEnabled="False">
                    <TextBlock Text="Disabled cache" />
                </RenderCache>
                <TextBlock>
                    <TextBlock.Background>
                        <CachedFillBrush SourceMode="{MGBinding Path=SourceMode}">
                            <SolidFillBrush Color="Blue" />
                        </CachedFillBrush>
                    </TextBlock.Background>
                </TextBlock>
                <StackPanel.Resources>
                    <ResourceDictionary>
                        <ResourceDictionary.Styles>
                            <Style TargetType="TextBlock">
                                <Setter Property="IsRenderVolatile" Value="True" />
                            </Style>
                        </ResourceDictionary.Styles>
                    </ResourceDictionary>
                </StackPanel.Resources>
            </StackPanel>
            """);
    }

    [Fact]
    public void CachedBrushSourceRequirementIsEnforcedByRuntimeInitialization()
    {
        string xaml = $"<CachedFillBrush xmlns=\"{XAMLParser.XMLLocalNameSpaceUri}\" />";

        AssertValid(xaml);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Portable.Xaml.XamlServices.Parse(xaml));
        Assert.Contains("CachedFillBrush.Brush is required", exception.Message);
    }

    [Theory]
    [InlineData("<RenderCache xmlns=\"{0}\" IsCacheEnabled=\"sometimes\" />")]
    [InlineData("<TextBlock xmlns=\"{0}\" IsRenderVolatile=\"sometimes\" />")]
    [InlineData("<TextBlock xmlns=\"{0}\"><TextBlock.Background><CachedFillBrush SourceMode=\"Always\" Brush=\"Red\" /></TextBlock.Background></TextBlock>")]
    [InlineData("<TextBlock xmlns=\"{0}\" UnknownCacheOption=\"True\" />")]
    [InlineData("<RenderCache xmlns=\"{0}\"><UnknownCacheElement /></RenderCache>")]
    public void InvalidCacheXamlDeclarationsAreRejected(string template)
    {
        Assert.NotEmpty(Validate(string.Format(template, XAMLParser.XMLLocalNameSpaceUri)));
    }

    [Fact]
    public void CheckedInSchemaMatchesGenerator()
    {
        XDocument checkedInSchema = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "mgui.xsd"));
        Assert.True(XNode.DeepEquals(checkedInSchema, new XamlSchemaGenerator().GenerateSchemaDocument()));
    }

    private static XmlSchemaSet CreateSchemas()
    {
        XmlSchemaSet schemas = new();
        using var reader = new XamlSchemaGenerator().GenerateSchemaDocument().CreateReader();
        schemas.Add(XAMLParser.XMLLocalNameSpaceUri, reader);
        schemas.Compile();
        return schemas;
    }

    private static void AssertValid(string xaml)
    {
        Assert.Empty(Validate(xaml));
    }

    private static List<string> Validate(string xaml)
    {
        List<string> errors = new();
        XDocument.Parse(xaml).Validate(Schemas, (_, args) => errors.Add(args.Message));
        return errors;
    }
}
