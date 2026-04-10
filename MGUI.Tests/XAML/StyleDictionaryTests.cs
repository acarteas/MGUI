using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
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

    private static MGResources CreateResources() => new(new MGTheme("TestFont"));

    private static NamedStyleScopeCollection CreateStyleScopes(IReadOnlyDictionary<string, Style> styles)
        => new(styles);

    private static void ProcessStyles(Element element)
    {
        try
        {
            ProcessStylesMethod.Invoke(element, new object[] { CreateResources() });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
