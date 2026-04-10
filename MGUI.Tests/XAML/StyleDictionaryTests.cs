using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.XAML;

public class StyleDictionaryTests
{
    [Fact]
    public void XAMLParser_LoadStyleDictionary_ParsesNamedStylesAndPreservesBasedOn()
    {
        string xaml = """
            <ResourceDictionary>
                <Style Name="BaseButton" TargetType="Button">
                    <Setter Property="Opacity" Value="0.5" />
                </Style>
                <Style Name="PrimaryButton" TargetType="Button" BasedOn="BaseButton">
                    <Setter Property="HorizontalAlignment" Value="Center" />
                </Style>
            </ResourceDictionary>
            """;

        ResourceDictionary dictionary = XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true);

        Assert.Equal(2, dictionary.Styles.Count);
        Assert.Equal("BaseButton", dictionary.Styles[0].Name);
        Assert.Equal("PrimaryButton", dictionary.Styles[1].Name);
        Assert.Equal("BaseButton", dictionary.Styles[1].BasedOn);
        Assert.Equal("Opacity", dictionary.Styles[0].Setters[0].Property);
        Assert.Equal("0.5", dictionary.Styles[0].Setters[0].Value);
        Assert.Equal("HorizontalAlignment", dictionary.Styles[1].Setters[0].Property);
    }

    [Fact]
    public void XAMLParser_LoadStyleDictionary_RejectsUnnamedStyles()
    {
        string xaml = """
            <ResourceDictionary>
                <Style TargetType="Button">
                    <Setter Property="Opacity" Value="0.5" />
                </Style>
            </ResourceDictionary>
            """;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true));

        Assert.Contains("named styles", ex.Message);
    }

    [Fact]
    public void MGResources_AddStyles_RegistersNamedStylesFromDictionary()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles =
            {
                new Style() { Name = "ActionButton", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "Opacity", Value = "0.5" } } }
            }
        };

        resources.AddStyles(dictionary);

        Assert.True(resources.Styles.ContainsKey("ActionButton"));
        Assert.Same(dictionary.Styles[0], resources.Styles["ActionButton"]);
    }

    [Fact]
    public void MGResources_AddStyles_ThrowsOnDuplicateStyleNames()
    {
        MGResources resources = CreateResources();
        resources.AddStyle(new Style() { Name = "ActionButton", TargetType = MGElementType.Button });

        ResourceDictionary dictionary = new()
        {
            Styles =
            {
                new Style() { Name = "ActionButton", TargetType = MGElementType.Button }
            }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => resources.AddStyles(dictionary));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void MGResources_AddStyles_AllowsIntraDictionaryBasedOnReferences()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles =
            {
                new Style() { Name = "BaseButton", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "Opacity", Value = "0.5" } } },
                new Style() { Name = "HotkeyButton", TargetType = MGElementType.Button, BasedOn = "BaseButton", Setters = { new Setter() { Property = "HorizontalAlignment", Value = "Center" } } }
            }
        };

        resources.AddStyles(dictionary);

        Assert.Equal(2, resources.Styles.Count);
    }

    [Fact]
    public void MGResources_AddStyles_ThrowsOnMissingBaseStyle()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles =
            {
                new Style() { Name = "HotkeyButton", TargetType = MGElementType.Button, BasedOn = "MissingBase" }
            }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => resources.AddStyles(dictionary));

        Assert.Contains("MissingBase", ex.Message);
    }

    [Fact]
    public void MGResources_AddStyles_ThrowsOnInheritanceCycles()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles =
            {
                new Style() { Name = "StyleA", TargetType = MGElementType.Button, BasedOn = "StyleB" },
                new Style() { Name = "StyleB", TargetType = MGElementType.Button, BasedOn = "StyleA" }
            }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => resources.AddStyles(dictionary));

        Assert.True(ex.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StyleResolver_ResolveSetters_AppliesBaseSettersBeforeDerivedSetters()
    {
        Dictionary<string, Style> stylesByName = new()
        {
            ["BaseButton"] = new Style()
            {
                Name = "BaseButton",
                TargetType = MGElementType.Button,
                Setters =
                {
                    new Setter() { Property = "Opacity", Value = "0.5" },
                    new Setter() { Property = "HorizontalAlignment", Value = "Left" }
                }
            },
            ["HotkeyButton"] = new Style()
            {
                Name = "HotkeyButton",
                TargetType = MGElementType.Button,
                BasedOn = "BaseButton",
                Setters =
                {
                    new Setter() { Property = "HorizontalAlignment", Value = "Center" }
                }
            }
        };

        IReadOnlyList<Setter> setters = StyleResolver.ResolveSetters("HotkeyButton", stylesByName["HotkeyButton"], stylesByName);

        Assert.Equal(new[] { "Opacity", "HorizontalAlignment", "HorizontalAlignment" }, setters.Select(x => x.Property).ToArray());
        Assert.Equal("0.5", setters[0].Value);
        Assert.Equal("Left", setters[1].Value);
        Assert.Equal("Center", setters[2].Value);
    }

    [Fact]
    public void StyleResolver_ResolveSetters_ThrowsOnTargetTypeMismatch()
    {
        Dictionary<string, Style> stylesByName = new()
        {
            ["BaseButton"] = new Style() { Name = "BaseButton", TargetType = MGElementType.Button },
            ["ButtonText"] = new Style() { Name = "ButtonText", TargetType = MGElementType.TextBlock, BasedOn = "BaseButton" }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StyleResolver.ResolveSetters("ButtonText", stylesByName["ButtonText"], stylesByName));

        Assert.Contains("targets", ex.Message);
    }

    [Fact]
    public void StyleResolver_ResolveSetters_AllowsLocalNamedStylesToDeriveFromGlobalNamedStyles()
    {
        Dictionary<string, Style> stylesByName = new()
        {
            ["ActionButton"] = new Style()
            {
                Name = "ActionButton",
                TargetType = MGElementType.Button,
                Setters =
                {
                    new Setter() { Property = "Opacity", Value = "0.5" }
                }
            },
            ["HotkeyButton"] = new Style()
            {
                Name = "HotkeyButton",
                TargetType = MGElementType.Button,
                BasedOn = "ActionButton",
                Setters =
                {
                    new Setter() { Property = "HorizontalAlignment", Value = "Center" }
                }
            }
        };

        IReadOnlyList<Setter> setters = StyleResolver.ResolveSetters("HotkeyButton", stylesByName["HotkeyButton"], stylesByName);

        Assert.Equal(2, setters.Count);
        Assert.Equal("Opacity", setters[0].Property);
        Assert.Equal("HorizontalAlignment", setters[1].Property);
    }

    [Fact]
    public void StyleResolver_ResolveExplicitStyles_PreservesStyleNamesOrdering()
    {
        Dictionary<string, Style> stylesByName = new()
        {
            ["First"] = new Style() { Name = "First", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "Opacity", Value = "0.25" } } },
            ["Second"] = new Style() { Name = "Second", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "HorizontalAlignment", Value = "Center" } } }
        };

        IReadOnlyList<ResolvedStyle> styles = StyleResolver.ResolveExplicitStyles("Second,First", MGElementType.Button, stylesByName);

        Assert.Equal(new[] { "Second", "First" }, styles.Select(x => x.Name).ToArray());
    }

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));
}
