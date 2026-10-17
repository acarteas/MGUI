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
