using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
#if UseWPF
using System.Xaml;
#else
using Portable.Xaml;
#endif

namespace MGUI.Tests.XAML;

public class StyleDictionaryTests
{
    private static readonly MethodInfo ProcessStylesMethod = typeof(Element)
        .GetMethod("ProcessStyles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MGResources) }, null)!;

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void XAMLParser_LoadStyleDictionary_ParsesKeyedColorsAndNamedStyles(bool sanitizeXamlString)
    {
        string xaml = sanitizeXamlString
            ? """
                <ResourceDictionary>
                    <Color Key="Accent" Value="#336699" />
                    <Style Name="ActionButton" TargetType="Button" />
                    <Color Key="Warning" Value="rgb(200, 100, 50)" />
                </ResourceDictionary>
                """
            : """
                <MGUI:ResourceDictionary xmlns:MGUI="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                    <MGUI:ColorResource Key="Accent" Value="#336699" />
                    <MGUI:Style Name="ActionButton" TargetType="Button" />
                    <MGUI:ColorResource Key="Warning" Value="rgb(200, 100, 50)" />
                </MGUI:ResourceDictionary>
                """;

        ResourceDictionary dictionary = XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString);

        Assert.Equal(new[] { "ColorResource", "Style", "ColorResource" }, dictionary.Entries.Select(x => x.GetType().Name));
        Assert.Equal("ActionButton", Assert.Single(dictionary.Styles).Name);
        Assert.Equal(new[] { "Accent", "Warning" }, dictionary.ColorResources.Select(x => x.Key));
        Assert.Equal(new XAMLColor(51, 102, 153), dictionary.ColorResources[0].Value);
    }

    [Fact]
    public void XAMLParser_LoadStyleDictionary_ParsesConciseColorWithoutSanitization()
    {
        const string xaml = """
            <MGUI:ResourceDictionary xmlns:MGUI="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <MGUI:Color Key="Accent" Value="#336699" />
            </MGUI:ResourceDictionary>
            """;

        ResourceDictionary dictionary = XAMLParser.LoadStyleDictionary(xaml);

        ColorResource color = Assert.Single(dictionary.ColorResources);
        Assert.Equal("Accent", color.Key);
        Assert.Equal(new XAMLColor(51, 102, 153), color.Value);
    }

    [Fact]
    public void XAMLParser_PrepareXAMLString_PreservesSanitizedAndExplicitColorResourceForms()
    {
        const string sanitizedXaml = """
            <ResourceDictionary>
                <Color Key="Accent" Value="#336699" />
            </ResourceDictionary>
            """;
        const string explicitXaml = """
            <MGUI:ResourceDictionary xmlns:MGUI="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <MGUI:ColorResource Key="Accent" Value="#336699" />
            </MGUI:ResourceDictionary>
            """;

        string sanitized = PrepareXamlString(sanitizedXaml, sanitizeXamlString: true);
        string sanitizedTwice = PrepareXamlString(sanitized, sanitizeXamlString: false);
        string explicitColorResource = PrepareXamlString(explicitXaml, sanitizeXamlString: false);

        Assert.Equal(sanitized, sanitizedTwice);
        Assert.Equal(explicitXaml, explicitColorResource);
        Assert.Equal(nameof(ColorResource), XDocument.Parse(sanitized).Root!.Elements().Single().Name.LocalName);
    }

    [Fact]
    public void XAMLParser_PrepareXAMLString_LeavesUnrelatedColorElementsUnchanged()
    {
        const string xaml = """
            <MGUI:StackPanel xmlns:MGUI="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:Other="urn:other">
                <MGUI:StackPanel.Resources>
                    <MGUI:Color Key="PropertyColor" Value="#336699" />
                </MGUI:StackPanel.Resources>
                <MGUI:ResourceDictionary>
                    <Other:Color Key="OtherColor" Value="#336699" />
                </MGUI:ResourceDictionary>
            </MGUI:StackPanel>
            """;

        XDocument prepared = XDocument.Parse(PrepareXamlString(xaml, sanitizeXamlString: false));
        XElement propertyColor = prepared.Descendants(XName.Get("Color", XAMLParser.XMLLocalNameSpaceUri)).Single();
        XElement otherColor = prepared.Descendants(XName.Get("Color", "urn:other")).Single();

        Assert.Equal("StackPanel.Resources", propertyColor.Parent!.Name.LocalName);
        Assert.Equal("ResourceDictionary", otherColor.Parent!.Name.LocalName);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void XAMLParser_LoadStyleDictionary_RejectsBlankColorKeys(string key)
    {
        string xaml = $"<ResourceDictionary><Color Key=\"{key}\" Value=\"Red\" /></ResourceDictionary>";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true));

        Assert.Contains(nameof(ColorResource.Key), ex.Message);
    }

    [Fact]
    public void XAMLParser_LoadStyleDictionary_RejectsDuplicateColorKeys()
    {
        const string xaml = "<ResourceDictionary><Color Key=\"Accent\" Value=\"Red\" /><Color Key=\"Accent\" Value=\"Blue\" /></ResourceDictionary>";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true));

        Assert.Contains("Accent", ex.Message);
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
    public void MGResources_AddResources_RegistersStylesAndColors()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles = { new Style() { Name = "ActionButton", TargetType = MGElementType.Button } },
            ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(51, 102, 153) } }
        };

        resources.AddResources(dictionary);

        Assert.Same(dictionary.Styles[0], resources.Styles["ActionButton"]);
        Assert.Equal(dictionary.ColorResources[0].Value, resources.Colors["Accent"]);
        Assert.True(resources.TryGetColor("Accent", out XAMLColor color));
        Assert.Equal(dictionary.ColorResources[0].Value, color);
    }

    [Fact]
    public void MGResources_RemoveColor_RemovesColorAndRaisesEvent()
    {
        MGResources resources = CreateResources();
        XAMLColor expectedColor = new(51, 102, 153);
        (string Key, XAMLColor Color)? removedColor = null;
        resources.OnColorRemoved += (_, value) => removedColor = value;
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "Accent", Value = expectedColor } }
        });

        bool result = resources.RemoveColor("Accent");

        Assert.True(result);
        Assert.False(resources.Colors.ContainsKey("Accent"));
        Assert.Equal(("Accent", expectedColor), removedColor);
    }

    [Fact]
    public void MGResources_RemoveColor_ReturnsFalseWithoutRaisingEventWhenKeyIsMissing()
    {
        MGResources resources = CreateResources();
        bool eventRaised = false;
        resources.OnColorRemoved += (_, _) => eventRaised = true;

        bool result = resources.RemoveColor("MissingAccent");

        Assert.False(result);
        Assert.False(eventRaised);
    }

    [Fact]
    public void MGResources_RemoveColor_AllowsReAddingKeyWithNewValue()
    {
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } }
        });

        Assert.True(resources.RemoveColor("Accent"));

        XAMLColor expectedColor = new(4, 5, 6);
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "Accent", Value = expectedColor } }
        });

        Assert.Equal(expectedColor, resources.Colors["Accent"]);
    }

    [Fact]
    public void MGResources_AddResources_RejectsDuplicateGlobalColorWithoutPartialRegistration()
    {
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } }
        });
        ResourceDictionary dictionary = new()
        {
            Styles = { new Style() { Name = "ActionButton", TargetType = MGElementType.Button } },
            ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(4, 5, 6) } }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => resources.AddResources(dictionary));

        Assert.Contains("Accent", ex.Message);
        Assert.False(resources.Styles.ContainsKey("ActionButton"));
        Assert.Single(resources.Colors);
    }

    [Fact]
    public void MGResources_AddResources_RejectsInvalidStylesWithoutRegisteringColors()
    {
        MGResources resources = CreateResources();
        ResourceDictionary dictionary = new()
        {
            Styles = { new Style() { TargetType = MGElementType.Button } },
            ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } }
        };

        Assert.Throws<InvalidOperationException>(() => resources.AddResources(dictionary));

        Assert.Empty(resources.Styles);
        Assert.Empty(resources.Colors);
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
        Dictionary<string, Style> sourceStyles = new()
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
        NamedStyleScopeCollection stylesByName = CreateStyleScopes(sourceStyles);

        IReadOnlyList<Setter> setters = StyleResolver.ResolveSetters("HotkeyButton", sourceStyles["HotkeyButton"], stylesByName);

        Assert.Equal(new[] { "Opacity", "HorizontalAlignment", "HorizontalAlignment" }, setters.Select(x => x.Property).ToArray());
        Assert.Equal("0.5", setters[0].Value);
        Assert.Equal("Left", setters[1].Value);
        Assert.Equal("Center", setters[2].Value);
    }

    [Fact]
    public void StyleResolver_ResolveSetters_ThrowsOnTargetTypeMismatch()
    {
        Dictionary<string, Style> sourceStyles = new()
        {
            ["BaseButton"] = new Style() { Name = "BaseButton", TargetType = MGElementType.Button },
            ["ButtonText"] = new Style() { Name = "ButtonText", TargetType = MGElementType.TextBlock, BasedOn = "BaseButton" }
        };
        NamedStyleScopeCollection stylesByName = CreateStyleScopes(sourceStyles);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StyleResolver.ResolveSetters("ButtonText", sourceStyles["ButtonText"], stylesByName));

        Assert.Contains("targets", ex.Message);
    }

    [Fact]
    public void StyleResolver_ResolveSetters_AllowsLocalNamedStylesToDeriveFromGlobalNamedStyles()
    {
        Dictionary<string, Style> sourceStyles = new()
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
        NamedStyleScopeCollection stylesByName = CreateStyleScopes(sourceStyles);

        IReadOnlyList<Setter> setters = StyleResolver.ResolveSetters("HotkeyButton", sourceStyles["HotkeyButton"], stylesByName);

        Assert.Equal(2, setters.Count);
        Assert.Equal("Opacity", setters[0].Property);
        Assert.Equal("HorizontalAlignment", setters[1].Property);
    }

    [Fact]
    public void StyleResolver_ResolveExplicitStyles_PreservesStyleNamesOrdering()
    {
        NamedStyleScopeCollection stylesByName = CreateStyleScopes(new Dictionary<string, Style>
        {
            ["First"] = new Style() { Name = "First", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "Opacity", Value = "0.25" } } },
            ["Second"] = new Style() { Name = "Second", TargetType = MGElementType.Button, Setters = { new Setter() { Property = "HorizontalAlignment", Value = "Center" } } }
        });

        IReadOnlyList<ResolvedStyle> styles = StyleResolver.ResolveExplicitStyles("Second,First", MGElementType.Button, stylesByName);

        Assert.Equal(new[] { "Second", "First" }, styles.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void WindowResources_ParsesNamedAndImplicitStyles()
    {
        string xaml = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <Window.Resources>
                    <ResourceDictionary>
                        <Style Name="HeaderText" TargetType="TextBlock">
                            <Setter Property="MinWidth" Value="40" />
                        </Style>
                        <Style TargetType="TextBlock">
                            <Setter Property="MaxWidth" Value="80" />
                        </Style>
                    </ResourceDictionary>
                </Window.Resources>
            </Window>
            """;

        Window parsed = (Window)XamlServices.Parse(xaml);

        Assert.Equal(2, parsed.Resources.Styles.Count);
        Assert.Equal("HeaderText", parsed.Resources.Styles[0].Name);
        Assert.Null(parsed.Resources.Styles[1].Name);
    }

    [Fact]
    public void ProcessStyles_WindowResources_ImplicitStyleAppliesToDescendants()
    {
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "20" } }
                    }
                }
            },
            Children = { new TextBlock() }
        };

        ProcessStyles(root);

        Assert.Equal(20, ((TextBlock)root.Children[0]).MinWidth);
    }

    [Fact]
    public void ProcessStyles_NestedResources_ImplicitStylesOverrideOuterScope()
    {
        StackPanel childPanel = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "30" } }
                    }
                }
            },
            Children = { new TextBlock() }
        };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "10" } }
                    }
                }
            },
            Children = { childPanel }
        };

        ProcessStyles(root);

        Assert.Equal(30, ((TextBlock)childPanel.Children[0]).MinWidth);
    }

    [Fact]
    public void ProcessStyles_NestedResources_NamedStylesShadowOuterScope()
    {
        TextBlock target = new() { StyleNames = "Header" };
        StackPanel childPanel = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        Name = "Header",
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "30" } }
                    }
                }
            },
            Children = { target }
        };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        Name = "Header",
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "10" } }
                    }
                }
            },
            Children = { childPanel }
        };

        ProcessStyles(root);

        Assert.Equal(30, target.MinWidth);
    }

    [Fact]
    public void ProcessStyles_LegacyStyles_ShadowSameNodeResources()
    {
        TextBlock target = new() { StyleNames = "Header" };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        Name = "Header",
                        TargetType = MGElementType.TextBlock,
                        Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "10" } }
                    }
                }
            },
            Styles =
            {
                new Style()
                {
                    Name = "Header",
                    TargetType = MGElementType.TextBlock,
                    Setters = { new Setter() { Property = nameof(Element.MinWidth), Value = "30" } }
                }
            },
            Children = { target }
        };

        ProcessStyles(root);

        Assert.Equal(30, target.MinWidth);
    }

    [Fact]
    public void ProcessStyles_ThrowsOnDuplicateNamedStylesWithinSingleResourceDictionary()
    {
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style() { Name = "Header", TargetType = MGElementType.TextBlock },
                    new Style() { Name = "Header", TargetType = MGElementType.TextBlock }
                }
            }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ProcessStyles(root));

        Assert.Contains("Header", ex.Message);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesDirectColorProperty()
    {
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } }
            },
            Children =
            {
                new Image() { TextureColor = ColorStringConverter.ParseColor("{StaticResource Accent}") }
            }
        };

        ProcessStyles(root);

        Assert.Equal(new XAMLColor(12, 34, 56), ((Image)root.Children[0]).TextureColor);
    }

    [Fact]
    public void XAMLParser_StaticResource_ParsesAndResolvesDirectColorProperty()
    {
        const string xaml = """
            <StackPanel xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <StackPanel.Resources>
                    <ResourceDictionary>
                        <ColorResource Key="Accent" Value="#0C2238" />
                    </ResourceDictionary>
                </StackPanel.Resources>
                <Image TextureColor="{StaticResource Accent}" />
            </StackPanel>
            """;

        StackPanel root = (StackPanel)XamlServices.Parse(xaml);

        ProcessStyles(root);

        Assert.Equal(new XAMLColor(12, 34, 56), ((Image)root.Children[0]).TextureColor);
    }

    [Fact]
    public void XAMLParser_StaticResource_ParsesSanitizedStyleSetter()
    {
        const string xaml = """
            <ResourceDictionary>
                <Color Key="Accent" Value="#0C2238" />
                <Style Name="AccentImage" TargetType="Image">
                    <Setter Property="TextureColor" Value="{StaticResource Accent}" />
                </Style>
            </ResourceDictionary>
            """;
        MGResources resources = CreateResources();
        resources.AddResources(XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true));
        Image target = new() { StyleNames = "AccentImage" };

        ProcessStyles(target, resources);

        Assert.Equal(new XAMLColor(12, 34, 56), target.TextureColor);
    }

    [Fact]
    public void XAMLParser_StaticResource_ResolvesStyleSettersForFillAndBorderBrushes()
    {
        const string xaml = """
            <ResourceDictionary>
                <Color Key="Accent" Value="#0C2238" />
                <Style Name="AccentBorder" TargetType="Border">
                    <Setter Property="Background" Value="{StaticResource Accent}" />
                    <Setter Property="BorderBrush" Value="{StaticResource Accent}" />
                </Style>
            </ResourceDictionary>
            """;
        MGResources resources = CreateResources();
        resources.AddResources(XAMLParser.LoadStyleDictionary(xaml, sanitizeXamlString: true));
        Border target = new() { StyleNames = "AccentBorder" };

        ProcessStyles(target, resources);

        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(target.Background).Color);
        UniformBorderBrush borderBrush = Assert.IsType<UniformBorderBrush>(target.BorderBrush);
        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(borderBrush.Brush).Color);
    }

    [Fact]
    public void XAMLParser_StaticResource_ResolvesAndMaterializesNestedFillBrushColor()
    {
        const string xaml = """
            <StackPanel xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <StackPanel.Resources>
                    <ResourceDictionary>
                        <ColorResource Key="Accent" Value="#0C2238" />
                    </ResourceDictionary>
                </StackPanel.Resources>
                <Border>
                    <Border.Background>
                        <SolidFillBrush Color="{StaticResource Accent}" />
                    </Border.Background>
                </Border>
            </StackPanel>
            """;
        StackPanel root = (StackPanel)XamlServices.Parse(xaml);

        ProcessStyles(root);

        Border border = (Border)root.Children[0];
        SolidFillBrush brush = Assert.IsType<SolidFillBrush>(border.Background);
        MGSolidFillBrush materializedBrush = Assert.IsType<MGSolidFillBrush>(brush.ToFillBrush(null, null));
        Assert.Equal(new XAMLColor(12, 34, 56), brush.Color);
        Assert.Equal(new Color(12, 34, 56), materializedBrush.Color);
    }

    [Fact]
    public void XAMLParser_StaticResource_ResolvesDirectFillAndBorderBrushColor()
    {
        const string xaml = """
            <StackPanel xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <StackPanel.Resources>
                    <ResourceDictionary>
                        <ColorResource Key="Accent" Value="#0C2238" />
                    </ResourceDictionary>
                </StackPanel.Resources>
                <Border Background="{StaticResource Accent}" BorderBrush="{StaticResource Accent}" />
                <Border>
                    <Border.BorderBrush>
                        <BandedBorderBrush>
                            <BorderBand><UniformBorderBrush><SolidFillBrush Color="{StaticResource Accent}" /></UniformBorderBrush></BorderBand>
                        </BandedBorderBrush>
                    </Border.BorderBrush>
                </Border>
            </StackPanel>
            """;
        StackPanel root = (StackPanel)XamlServices.Parse(xaml);

        ProcessStyles(root);

        Border border = (Border)root.Children[0];
        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(border.Background).Color);
        UniformBorderBrush borderBrush = Assert.IsType<UniformBorderBrush>(border.BorderBrush);
        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(borderBrush.Brush).Color);

        Border bandedBorder = (Border)root.Children[1];
        BandedBorderBrush bandedBorderBrush = Assert.IsType<BandedBorderBrush>(bandedBorder.BorderBrush);
        UniformBorderBrush bandBrush = Assert.IsType<UniformBorderBrush>(Assert.Single(bandedBorderBrush.Bands).Brush);
        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(bandBrush.Brush).Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_NestedFillBrushUsesNearestScopeAndGlobalFallback()
    {
        Border localTarget = new()
        {
            Background = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource Accent}"))
        };
        Border globalTarget = new()
        {
            Background = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource GlobalAccent}"))
        };
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } }
            },
            Children =
            {
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(4, 5, 6) } }
                    },
                    Children = { localTarget, globalTarget }
                }
            }
        };
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "GlobalAccent", Value = new XAMLColor(7, 8, 9) } }
        });

        ProcessStyles(root, resources);

        Assert.Equal(new XAMLColor(4, 5, 6), Assert.IsType<SolidFillBrush>(localTarget.Background).Color);
        Assert.Equal(new XAMLColor(7, 8, 9), Assert.IsType<SolidFillBrush>(globalTarget.Background).Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ReportsMissingNestedColorKeyAndPath()
    {
        Border target = new()
        {
            Background = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource MissingAccent}"))
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ProcessStyles(target));

        Assert.Contains("MissingAccent", ex.Message);
        Assert.Contains("Border.Background.Color", ex.Message);
        Assert.Contains(nameof(XAMLColor), ex.Message);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesNamedStyleSetterNestedFillBrushColor()
    {
        Border target = new() { StyleNames = "AccentBorder" };
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles =
                {
                    new Style()
                    {
                        Name = "AccentBorder",
                        TargetType = MGElementType.Border,
                        Setters = { new Setter() { Property = nameof(Border.Background), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource Accent}")) } }
                    }
                }
            },
            Children = { target }
        };

        ProcessStyles(root);

        SolidFillBrush brush = Assert.IsType<SolidFillBrush>(target.Background);
        MGSolidFillBrush materializedBrush = Assert.IsType<MGSolidFillBrush>(brush.ToFillBrush(null, null));
        Assert.Equal(new XAMLColor(12, 34, 56), brush.Color);
        Assert.Equal(new Color(12, 34, 56), materializedBrush.Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesImplicitStyleSetterNestedFillBrushColor()
    {
        Border target = new();
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.Border,
                        Setters = { new Setter() { Property = nameof(Border.Background), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource Accent}")) } }
                    }
                }
            },
            Children = { target }
        };

        ProcessStyles(root);

        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(target.Background).Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesBasedOnStyleSetterNestedFillBrushColor()
    {
        Border target = new() { StyleNames = "AccentBorder" };
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles =
                {
                    new Style()
                    {
                        Name = "BaseBorder",
                        TargetType = MGElementType.Border,
                        Setters = { new Setter() { Property = nameof(Border.Background), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource Accent}")) } }
                    },
                    new Style() { Name = "AccentBorder", TargetType = MGElementType.Border, BasedOn = "BaseBorder" }
                }
            },
            Children = { target }
        };

        ProcessStyles(root);

        Assert.Equal(new XAMLColor(12, 34, 56), Assert.IsType<SolidFillBrush>(target.Background).Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_StyleSetterNestedFillBrushUsesNearestScopeAndGlobalFallback()
    {
        Border localTarget = new();
        Border globalTarget = new();
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } },
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.Border,
                        Setters =
                        {
                            new Setter() { Property = nameof(Border.Background), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource Accent}")) },
                            new Setter() { Property = nameof(Border.DisabledBackground), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource GlobalAccent}")) }
                        }
                    }
                }
            },
            Children =
            {
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(4, 5, 6) } }
                    },
                    Children = { localTarget, globalTarget }
                }
            }
        };
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "GlobalAccent", Value = new XAMLColor(7, 8, 9) } }
        });

        ProcessStyles(root, resources);

        Assert.Equal(new XAMLColor(4, 5, 6), Assert.IsType<SolidFillBrush>(localTarget.Background).Color);
        Assert.Equal(new XAMLColor(7, 8, 9), Assert.IsType<SolidFillBrush>(globalTarget.DisabledBackground).Color);
    }

    [Fact]
    public void ProcessStyles_StaticResource_StyleSetterNestedFillBrushResolvesIndependentlyPerTargetScope()
    {
        Border firstTarget = new();
        Border secondTarget = new();
        SolidFillBrush sourceBrush = new(ColorStringConverter.ParseColor("{StaticResource Accent}"));
        Style sourceStyle = new()
        {
            TargetType = MGElementType.Border,
            Setters =
            {
                new Setter()
                {
                    Property = nameof(Border.Background),
                    Value = sourceBrush
                }
            }
        };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    sourceStyle
                }
            },
            Children =
            {
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources =
                        {
                            new ColorResource()
                            {
                                Key = "Accent",
                                Value = new XAMLColor(10, 20, 30)
                            }
                        }
                    },
                    Children = { firstTarget }
                },
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources =
                        {
                            new ColorResource()
                            {
                                Key = "Accent",
                                Value = new XAMLColor(40, 50, 60)
                            }
                        }
                    },
                    Children = { secondTarget }
                }
            }
        };

        ProcessStyles(root);

        SolidFillBrush firstBrush = Assert.IsType<SolidFillBrush>(firstTarget.Background);
        SolidFillBrush secondBrush = Assert.IsType<SolidFillBrush>(secondTarget.Background);
        Assert.NotSame(firstBrush, secondBrush);
        Assert.NotSame(sourceBrush, firstBrush);
        Assert.NotSame(sourceBrush, secondBrush);
        Assert.Equal("Accent", sourceBrush.Color.StaticResourceKey);
        Assert.Equal(
            new XAMLColor(10, 20, 30),
            firstBrush.Color
        );
        Assert.Equal(
            new XAMLColor(40, 50, 60),
            secondBrush.Color
        );
    }

    [Fact]
    public void ProcessStyles_StaticResource_StyleSetterEffectBrushParameterResolvesIndependentlyPerTargetScope()
    {
        Border firstTarget = new() { StyleNames = "AccentBorder" };
        Border secondTarget = new() { StyleNames = "AccentBorder" };
        EffectParameter sourceParameter = new()
        {
            Name = "Accent",
            Type = MGEffectParameterType.Color,
            Value = "{StaticResource Accent}"
        };
        EffectFillBrush sourceBrush = new()
        {
            EffectName = "Effect",
            Parameters = { sourceParameter }
        };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        Name = "BaseAccentBorder",
                        TargetType = MGElementType.Border,
                        Setters =
                        {
                            new Setter()
                            {
                                Property = nameof(Border.Background),
                                Value = sourceBrush
                            }
                        }
                    },
                    new Style()
                    {
                        Name = "AccentBorder",
                        TargetType = MGElementType.Border,
                        BasedOn = "BaseAccentBorder"
                    }
                }
            },
            Children =
            {
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(10, 20, 30) } }
                    },
                    Children = { firstTarget }
                },
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(40, 50, 60) } }
                    },
                    Children = { secondTarget }
                }
            }
        };

        ProcessStyles(root);

        EffectFillBrush firstBrush = Assert.IsType<EffectFillBrush>(firstTarget.Background);
        EffectFillBrush secondBrush = Assert.IsType<EffectFillBrush>(secondTarget.Background);
        EffectParameter firstParameter = Assert.Single(firstBrush.Parameters);
        EffectParameter secondParameter = Assert.Single(secondBrush.Parameters);
        Assert.NotSame(firstBrush, secondBrush);
        Assert.NotSame(firstParameter, secondParameter);
        Assert.NotSame(sourceParameter, firstParameter);
        Assert.Equal("{StaticResource Accent}", sourceParameter.Value);
        Assert.Equal(new XAMLColor(10, 20, 30).ToXNAColor().ToVector4(), firstParameter.ToParameterValue().Value);
        Assert.Equal(new XAMLColor(40, 50, 60).ToXNAColor().ToVector4(), secondParameter.ToParameterValue().Value);
    }

    [Fact]
    public void ProcessStyles_StaticResource_StyleSetterChildElementUsesItsOwnColorResourceScope()
    {
        Border target = new();
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } },
                Styles =
                {
                    new Style()
                    {
                        TargetType = MGElementType.Border,
                        Setters =
                        {
                            new Setter()
                            {
                                Property = nameof(Border.Content),
                                Value = new Image()
                                {
                                    Resources = new()
                                    {
                                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(4, 5, 6) } }
                                    },
                                    TextureColor = ColorStringConverter.ParseColor("{StaticResource Accent}")
                                }
                            }
                        }
                    }
                }
            },
            Children = { target }
        };

        ProcessStyles(root);

        Image child = Assert.IsType<Image>(target.Content);
        Assert.Equal(new XAMLColor(4, 5, 6), child.TextureColor);
    }

    [Fact]
    public void ProcessStyles_StaticResource_StyleSetterReportsMissingNestedColorKeyAndPath()
    {
        Border target = new() { StyleNames = "MissingAccentBorder" };
        StackPanel root = new()
        {
            Resources = new()
            {
                Styles =
                {
                    new Style()
                    {
                        Name = "MissingAccentBorder",
                        TargetType = MGElementType.Border,
                        Setters = { new Setter() { Property = nameof(Border.Background), Value = new SolidFillBrush(ColorStringConverter.ParseColor("{StaticResource MissingAccent}")) } }
                    }
                }
            },
            Children = { target }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ProcessStyles(root));

        Assert.Contains("MissingAccent", ex.Message);
        Assert.Contains("Border.Background.Color", ex.Message);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesNamedAndImplicitStyleSettersIncludingBasedOn()
    {
        Image namedTarget = new() { StyleNames = "AccentImage" };
        Image implicitTarget = new();
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles =
                {
                    new Style()
                    {
                        Name = "BaseImage",
                        TargetType = MGElementType.Image,
                        Setters = { new Setter() { Property = nameof(Image.TextureColor), Value = "{StaticResource Accent}" } }
                    },
                    new Style() { Name = "AccentImage", TargetType = MGElementType.Image, BasedOn = "BaseImage" },
                    new Style()
                    {
                        TargetType = MGElementType.Image,
                        Setters = { new Setter() { Property = nameof(Image.HoveredTextureColor), Value = "{StaticResource Accent}" } }
                    }
                }
            },
            Children = { namedTarget, implicitTarget }
        };

        ProcessStyles(root);

        Assert.Equal(new XAMLColor(12, 34, 56), namedTarget.TextureColor);
        Assert.Equal(new XAMLColor(12, 34, 56), namedTarget.HoveredTextureColor);
        Assert.Equal(new XAMLColor(12, 34, 56), implicitTarget.HoveredTextureColor);
    }

    [Fact]
    public void ProcessStyles_StaticResource_UsesNearestColorScopeThenGlobalFallback()
    {
        Image nestedTarget = new() { TextureColor = ColorStringConverter.ParseColor("{StaticResource Accent}") };
        Image fallbackTarget = new() { TextureColor = ColorStringConverter.ParseColor("{StaticResource GlobalAccent}") };
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(1, 2, 3) } }
            },
            Children =
            {
                new StackPanel()
                {
                    Resources = new()
                    {
                        ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(4, 5, 6) } }
                    },
                    Children = { nestedTarget, fallbackTarget }
                }
            }
        };
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary()
        {
            ColorResources = { new ColorResource() { Key = "GlobalAccent", Value = new XAMLColor(7, 8, 9) } }
        });

        ProcessStyles(root, resources);

        Assert.Equal(new XAMLColor(4, 5, 6), nestedTarget.TextureColor);
        Assert.Equal(new XAMLColor(7, 8, 9), fallbackTarget.TextureColor);
    }

    [Fact]
    public void ProcessStyles_StaticResource_PreservesLiteralColorValues()
    {
        Image target = new() { TextureColor = ColorStringConverter.ParseColor("Red") };

        ProcessStyles(target);

        Assert.Equal(ColorStringConverter.ParseColor("Red"), target.TextureColor);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ReportsMissingKeyAndTargetPath()
    {
        Image target = new() { TextureColor = ColorStringConverter.ParseColor("{StaticResource MissingAccent}") };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ProcessStyles(target));

        Assert.Contains("MissingAccent", ex.Message);
        Assert.Contains("Image.TextureColor", ex.Message);
        Assert.Contains("Nullable<XAMLColor>", ex.Message);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ReportsIncompatibleSetterTarget()
    {
        Image target = new() { StyleNames = "BadStyle" };
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource() { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles =
                {
                    new Style()
                    {
                        Name = "BadStyle",
                        TargetType = MGElementType.Image,
                        Setters = { new Setter() { Property = nameof(Element.Opacity), Value = "{StaticResource Accent}" } }
                    }
                }
            },
            Children = { target }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => ProcessStyles(root));

        Assert.Contains("Accent", ex.Message);
        Assert.Contains("Image.Opacity", ex.Message);
        Assert.Contains("Nullable<Single>", ex.Message);
        Assert.Contains(nameof(XAMLColor), ex.Message);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesColorEffectParametersInParsedImageAndBrushes()
    {
        const string xaml = """
            <StackPanel xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
                <StackPanel.Resources><ResourceDictionary><ColorResource Key="Accent" Value="#0C2238" /></ResourceDictionary></StackPanel.Resources>
                <Image SourceName="Image"><EffectParameter Name="ImageAccent" Type="Color" Value="{StaticResource Accent}" /></Image>
                <Border>
                    <Border.Background><EffectFillBrush EffectName="Effect"><EffectParameter Name="BrushAccent" Type="Color" Value="{StaticResource Accent}" /></EffectFillBrush></Border.Background>
                    <Border.DisabledBackground><NineSliceFillBrush SourceName="Frame" EffectName="Effect"><EffectParameter Name="NineSliceAccent" Type="Color" Value="{StaticResource Accent}" /></NineSliceFillBrush></Border.DisabledBackground>
                </Border>
            </StackPanel>
            """;
        StackPanel root = Assert.IsType<StackPanel>(XamlServices.Parse(xaml));

        ProcessStyles(root);

        EffectParameter imageParameter = Assert.Single(Assert.IsType<Image>(root.Children[0]).Parameters);
        Border border = Assert.IsType<Border>(root.Children[1]);
        EffectParameter effectParameter = Assert.Single(Assert.IsType<EffectFillBrush>(border.Background).Parameters);
        EffectParameter nineSliceParameter = Assert.Single(Assert.IsType<NineSliceFillBrush>(border.DisabledBackground).Parameters);
        Vector4 expected = new XAMLColor(12, 34, 56).ToXNAColor().ToVector4();
        Assert.Equal("{StaticResource Accent}", imageParameter.Value);
        Assert.Equal(expected, imageParameter.ToParameterValue().Value);
        Assert.Equal(expected, effectParameter.ToParameterValue().Value);
        Assert.Equal(expected, nineSliceParameter.ToParameterValue().Value);
    }

    [Fact]
    public void ProcessStyles_StaticResource_EffectParametersUseNearestScopeAndGlobalFallback()
    {
        Image localImage = new() { Parameters = { new EffectParameter { Name = "LocalAccent", Type = MGEffectParameterType.Color, Value = "{StaticResource Accent}" } } };
        Image globalImage = new() { Parameters = { new EffectParameter { Name = "GlobalAccent", Type = MGEffectParameterType.Color, Value = "{StaticResource GlobalAccent}" } } };
        StackPanel root = new()
        {
            Resources = new() { ColorResources = { new ColorResource { Key = "Accent", Value = new XAMLColor(1, 2, 3) } } },
            Children = { new StackPanel { Resources = new() { ColorResources = { new ColorResource { Key = "Accent", Value = new XAMLColor(4, 5, 6) } } }, Children = { localImage, globalImage } } }
        };
        MGResources resources = CreateResources();
        resources.AddResources(new ResourceDictionary { ColorResources = { new ColorResource { Key = "GlobalAccent", Value = new XAMLColor(7, 8, 9) } } });

        ProcessStyles(root, resources);

        Assert.Equal(new XAMLColor(4, 5, 6).ToXNAColor().ToVector4(), localImage.Parameters[0].ToParameterValue().Value);
        Assert.Equal(new XAMLColor(7, 8, 9).ToXNAColor().ToVector4(), globalImage.Parameters[0].ToParameterValue().Value);
    }

    [Fact]
    public void ProcessStyles_StaticResource_ResolvesStyleSuppliedEffectBrushParameter()
    {
        Border target = new();
        StackPanel root = new()
        {
            Resources = new()
            {
                ColorResources = { new ColorResource { Key = "Accent", Value = new XAMLColor(12, 34, 56) } },
                Styles = { new Style { TargetType = MGElementType.Border, Setters = { new Setter { Property = nameof(Border.Background), Value = new EffectFillBrush { EffectName = "Effect", Parameters = { new EffectParameter { Name = "Accent", Type = MGEffectParameterType.Color, Value = "{StaticResource Accent}" } } } } } } }
            },
            Children = { target }
        };

        ProcessStyles(root);

        EffectParameter parameter = Assert.Single(Assert.IsType<EffectFillBrush>(target.Background).Parameters);
        Assert.Equal(new XAMLColor(12, 34, 56).ToXNAColor().ToVector4(), parameter.ToParameterValue().Value);
    }

    [Theory]
    [InlineData("Missing", MGEffectParameterType.Color)]
    [InlineData("Accent", null)]
    [InlineData("Accent", MGEffectParameterType.Float)]
    public void ProcessStyles_StaticResource_EffectParameterErrorsIdentifyParameterAndResource(string key, MGEffectParameterType? type)
    {
        Image image = new() { Parameters = { new EffectParameter { Name = "AccentParameter", Type = type, Value = $"{{StaticResource {key}}}" } } };
        StackPanel root = new() { Resources = new() { ColorResources = { new ColorResource { Key = "Accent", Value = new XAMLColor(1, 2, 3) } } }, Children = { image } };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ProcessStyles(root));

        Assert.Contains("AccentParameter", exception.Message);
        Assert.Contains(key, exception.Message);
        Assert.Contains("Color", exception.Message);
    }

    [Fact]
    public void EffectParameter_LiteralColorAndVectorValuesRemainCompatible()
    {
        EffectParameter color = new() { Name = "Accent", Value = "#0C2238" };
        EffectParameter vector = new() { Name = "Direction", Type = MGEffectParameterType.Vector2, Value = "1, 0" };

        Assert.Equal(new XAMLColor(12, 34, 56).ToXNAColor().ToVector4(), color.ToParameterValue().Value);
        Assert.Equal(new Vector2(1, 0), vector.ToParameterValue().Value);
    }

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static NamedStyleScopeCollection CreateStyleScopes(IReadOnlyDictionary<string, Style> styles)
        => new(styles);

    private static string PrepareXamlString(string xaml, bool sanitizeXamlString)
    {
        MethodInfo method = typeof(XAMLParser).GetMethod("PrepareXAMLString", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)method.Invoke(null, new object[] { xaml, sanitizeXamlString, false })!;
    }

    private static void ProcessStyles(Element element)
        => ProcessStyles(element, CreateResources());

    private static void ProcessStyles(Element element, MGResources resources)
    {
        try
        {
            ProcessStylesMethod.Invoke(element, new object[] { resources });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
