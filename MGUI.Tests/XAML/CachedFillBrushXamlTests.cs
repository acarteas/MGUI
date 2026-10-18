using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Data_Binding;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;
using XamlBorder = MGUI.Core.UI.XAML.Border;
using XamlEffectParameter = MGUI.Core.UI.XAML.EffectParameter;

namespace MGUI.Tests.XAML;

[Collection("Render cache clock")]
public sealed class CachedFillBrushXamlTests
{
    private const string Namespace = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    [Theory]
    [InlineData("<SolidFillBrush Color=\"Red\" />")]
    [InlineData("<CachedFillBrush.Brush><SolidFillBrush Color=\"Red\" /></CachedFillBrush.Brush>")]
    public void ContentAndPropertySyntaxPreserveRuntimeDefaults(string content)
    {
        CachedFillBrush source = Parse($"<CachedFillBrush xmlns=\"{Namespace}\">{content}</CachedFillBrush>");
        Assert.Null(source.IsCacheEnabled);
        Assert.Null(source.SourceMode);
        MGCachedFillBrush result = Materialize(source);
        Assert.True(result.IsCacheEnabled);
        Assert.Equal(MGRenderCacheSourceMode.Tracked, result.SourceMode);
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(result.Brush).Color);
    }

    [Fact]
    public void MissingBrushFailsParsingAndProgrammaticMaterializationClearly()
    {
        Exception parsedError = Assert.ThrowsAny<Exception>(() => Parse($"<CachedFillBrush xmlns=\"{Namespace}\" />"));
        Assert.Contains("CachedFillBrush.Brush", parsedError.ToString());
        Assert.Contains("source brush", parsedError.ToString());
        InvalidOperationException runtimeError = Assert.Throws<InvalidOperationException>(() => Materialize(new CachedFillBrush()));
        Assert.Contains("CachedFillBrush.Brush", runtimeError.Message);
    }

    [Fact]
    public void InheritedStyleCopiesResolveNestedResourcesInEveryFillSlot()
    {
        string xaml = $$"""
            <StackPanel xmlns="{{Namespace}}">
              <StackPanel.Resources>
                <ResourceDictionary>
                  <ColorResource Key="Accent" Value="#123456" />
                  <Style Name="Base" TargetType="Border">
                    <Setter Property="Background"><Setter.Value>
                      <CachedFillBrush SourceMode="ExplicitlyStatic" IsCacheEnabled="False">
                        <EffectFillBrush EffectName="Missing">
                          <EffectFillBrush.FallbackBrush><SolidFillBrush Color="{StaticResource Accent}" /></EffectFillBrush.FallbackBrush>
                        </EffectFillBrush>
                      </CachedFillBrush>
                    </Setter.Value></Setter>
                  </Style>
                  <Style Name="Derived" TargetType="Border" BasedOn="Base" />
                </ResourceDictionary>
              </StackPanel.Resources>
              <Border StyleNames="Derived">
                <Border.Overlay><CachedFillBrush><TextureFillBrush SourceName="Tile" Color="{StaticResource Accent}" /></CachedFillBrush></Border.Overlay>
                <Border.DisabledBackground><CachedFillBrush><SolidFillBrush Color="{StaticResource Accent}" /></CachedFillBrush></Border.DisabledBackground>
                <Border.SelectedBackground><CachedFillBrush><SolidFillBrush Color="{StaticResource Accent}" /></CachedFillBrush></Border.SelectedBackground>
              </Border>
              <Border StyleNames="Derived" />
            </StackPanel>
            """;
        StackPanel root = Assert.IsType<StackPanel>(XamlServices.Parse(xaml));
        MGResources resources = new(new MGTheme("TestFont"));
        Texture2D texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        resources.AddTexture("Tile", new MGTextureData(texture));
        ProcessStyles(root, resources);
        XamlBorder first = Assert.IsType<XamlBorder>(root.Children[0]);
        XamlBorder second = Assert.IsType<XamlBorder>(root.Children[1]);
        CachedFillBrush firstSource = Assert.IsType<CachedFillBrush>(first.Background);
        CachedFillBrush secondSource = Assert.IsType<CachedFillBrush>(second.Background);
        Assert.NotSame(firstSource, secondSource);
        Assert.NotSame(firstSource.Brush, secondSource.Brush);
        Color expected = new(18, 52, 86);
        foreach (FillBrush slot in new[] { first.Background, first.DisabledBackground, first.SelectedBackground, second.Background })
        {
            MGCachedFillBrush runtime = Materialize(Assert.IsType<CachedFillBrush>(slot), resources);
            Assert.Equal(expected, Assert.IsType<MGSolidFillBrush>(runtime.Brush).Color);
        }
        MGCachedFillBrush background = Materialize(firstSource, resources);
        Assert.False(background.IsCacheEnabled);
        Assert.Equal(MGRenderCacheSourceMode.ExplicitlyStatic, background.SourceMode);
        MGTextureFillBrush overlay = Assert.IsType<MGTextureFillBrush>(Materialize(Assert.IsType<CachedFillBrush>(first.Overlay), resources).Brush);
        Assert.Same(texture, overlay.Source.Texture);
        Assert.Equal(expected, overlay.Color);
    }

    [Fact]
    public void WholeBrushStaticResourceResolvesBeforeMaterialization()
    {
        XamlBorder root = Assert.IsType<XamlBorder>(XamlServices.Parse($$"""
            <Border xmlns="{{Namespace}}">
              <Border.Resources><ResourceDictionary><ColorResource Key="Accent" Value="#123456" /></ResourceDictionary></Border.Resources>
              <Border.Background><CachedFillBrush Brush="{StaticResource Accent}" /></Border.Background>
            </Border>
            """));
        MGResources resources = new(new MGTheme("TestFont"));
        ProcessStyles(root, resources);
        Assert.Equal(new Color(18, 52, 86), Assert.IsType<MGSolidFillBrush>(Materialize(Assert.IsType<CachedFillBrush>(root.Background), resources).Brush).Color);
    }

    [Fact]
    public void BrushBindingRequiresInitialSourceClearly()
    {
        Exception error = Assert.ThrowsAny<Exception>(() => Parse($$"""
            <CachedFillBrush xmlns="{{Namespace}}" Brush="{MGBinding Source}" />
            """));
        Assert.Contains("Brush binding requires initial source brush", error.ToString());
    }

    [Fact]
    public void PropertyObserversSeeAlreadyInvalidatedAndReconciledState()
    {
        MGCachedFillBrush brush = new(new MGSolidFillBrush(Color.Red));
        ulong revision = brush.VisualRevision;
        int changes = 0;
        brush.PropertyChanged += (_, _) =>
        {
            Assert.True(brush.VisualRevision > revision);
            revision = brush.VisualRevision;
            Assert.Equal(brush.SourceMode == MGRenderCacheSourceMode.ExplicitlyStatic, brush.SupportsRenderCaching);
            changes++;
        };
        brush.Brush = new UnsupportedBrush();
        brush.IsCacheEnabled = false;
        brush.SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic;
        Assert.Equal(3, changes);
    }

    [Fact]
    public void NestedEffectAndParameterResourcesMaterializeResolvedValues()
    {
        string xaml = $$"""
            <Border xmlns="{{Namespace}}">
              <Border.Resources><ResourceDictionary><ColorResource Key="Accent" Value="#123456" /></ResourceDictionary></Border.Resources>
              <Border.Background>
                <CachedFillBrush SourceMode="ExplicitlyStatic">
                  <EffectFillBrush EffectName="Tint">
                    <EffectParameter Name="TintColor" Type="Color" Value="{StaticResource Accent}" />
                  </EffectFillBrush>
                </CachedFillBrush>
              </Border.Background>
            </Border>
            """;
        XamlBorder root = Assert.IsType<XamlBorder>(XamlServices.Parse(xaml));
        MGResources resources = new(new MGTheme("TestFont"));
        Effect effect = (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        resources.AddEffect("Tint", effect);
        ProcessStyles(root, resources);
        MGEffectFillBrush inner = Assert.IsType<MGEffectFillBrush>(Materialize(Assert.IsType<CachedFillBrush>(root.Background), resources).Brush);
        Assert.Same(effect, inner.Effect);
        Assert.Equal(new Color(18, 52, 86).ToVector4(), Assert.Single(inner.Parameters).Value);
    }

    [Fact]
    public void ParsedBindingsUpdateSettingsAndSourceAndInvalidateOnlyChanges()
    {
        CachedFillBrush source = Parse($$"""
            <CachedFillBrush xmlns="{{Namespace}}" IsCacheEnabled="{MGBinding Enabled}" SourceMode="{MGBinding Mode}">
              <SolidFillBrush Color="Red" />
            </CachedFillBrush>
            """);
        CachedFillBrush sourceCopy = ObjectUtils.Copy(source);
        Assert.NotSame(source.Bindings, sourceCopy.Bindings);
        Assert.Equal(source.Bindings.Select(binding => (binding.TargetPath, binding.SourcePath)),
            sourceCopy.Bindings.Select(binding => (binding.TargetPath, binding.SourcePath)));
        MGCachedFillBrush brush = Materialize(sourceCopy);
        Settings settings = new();
        brush.DataContext = settings;
        List<DataBinding> bindings = sourceCopy.Bindings.Select(config => DataBindingManager.AddBinding(config, brush)).ToList();
        try
        {
            int changes = 0;
            brush.VisualInvalidated += (_, _) => changes++;
            settings.Enabled = false;
            settings.Mode = MGRenderCacheSourceMode.ExplicitlyStatic;
            Assert.False(brush.IsCacheEnabled);
            Assert.Equal(settings.Mode, brush.SourceMode);
            Assert.Equal(2, changes);
            settings.Enabled = false;
            settings.Mode = MGRenderCacheSourceMode.ExplicitlyStatic;
            Assert.Equal(2, changes);
            Settings replacement = new() { Enabled = true, Mode = MGRenderCacheSourceMode.Tracked };
            brush.DataContext = replacement;
            brush.InvokeDataContextChanged();
            Assert.True(brush.IsCacheEnabled);
            Assert.Equal(MGRenderCacheSourceMode.Tracked, brush.SourceMode);
            Assert.Equal(4, changes);
            settings.Enabled = false;
            Assert.True(brush.IsCacheEnabled);
            Assert.Equal(4, changes);
            MGCachedFillBrush copy = Assert.IsType<MGCachedFillBrush>(brush.Copy());
            Assert.Null(copy.DataContext);
            Assert.Empty(copy.Bindings);
            Assert.Null(copy.SourceElement);
            Assert.DoesNotContain(DataBindingManager.Bindings, binding => ReferenceEquals(binding.TargetObject, copy));
            replacement.Enabled = false;
            Assert.True(copy.IsCacheEnabled);
        }
        finally
        {
            foreach (DataBinding binding in bindings)
            {
                DataBindingManager.RemoveBinding(binding);
            }
        }
    }

    [Fact]
    public void SourceReplacementNotifiesAndWeakHostDoesNotRetainOwner()
    {
        MGCachedFillBrush brush = Materialize(Parse($"<CachedFillBrush xmlns=\"{Namespace}\" Brush=\"Red\" />"));
        List<string?> notifications = new();
        brush.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        int invalidations = 0;
        brush.VisualInvalidated += (_, _) => invalidations++;
        brush.Brush = new MGSolidFillBrush(Color.Blue);
        brush.Brush = new MGSolidFillBrush(Color.Blue);
        Assert.Equal(new[] { nameof(brush.Brush) }, notifications);
        Assert.Equal(1, invalidations);
        WeakReference owner = AttachOwner(brush);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(owner.IsAlive);
        Assert.Null(brush.SourceElement);
        Assert.Null(brush.GetDesktop());
        Assert.Null(brush.GetResources());
        Assert.False(brush.TryGetElementByName("Missing", out MGElement named));
        Assert.Null(named);
        GC.KeepAlive(brush);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachOwner(MGCachedFillBrush brush)
    {
        MGWindow owner = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
        MGDesktop desktop = CreateDesktop(new(new MGTheme("TestFont")));
        typeof(MGWindow).GetField("<Desktop>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, desktop);
        typeof(MGElement).GetField("<ElementType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, MGElementType.Window);
        brush.SourceElement = owner;
        Assert.Same(desktop, brush.GetDesktop());
        Assert.Same(desktop.Resources, brush.GetResources());
        return new WeakReference(owner);
    }

    private static CachedFillBrush Parse(string xaml) => Assert.IsType<CachedFillBrush>(XamlServices.Parse(xaml));
    private static MGCachedFillBrush Materialize(CachedFillBrush source, MGResources? resources = null)
        => Assert.IsType<MGCachedFillBrush>(source.ToFillBrush(CreateDesktop(resources ?? new(new MGTheme("TestFont"))), null));
    private static MGDesktop CreateDesktop(MGResources resources)
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        typeof(MGDesktop).GetField("<Resources>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(desktop, resources);
        return desktop;
    }
    private static void ProcessStyles(Element root, MGResources resources)
        => typeof(Element).GetMethod("ProcessStyles", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MGResources) }, null)!.Invoke(root, new object[] { resources });

    private sealed class UnsupportedBrush : IFillBrush
    {
        public IFillBrush Copy() => new UnsupportedBrush();
        public void Draw(ElementDrawArgs args, MGElement element, Microsoft.Xna.Framework.Rectangle bounds) { }
    }

    private sealed class Settings : INotifyPropertyChanged
    {
        private bool enabled = true;
        private MGRenderCacheSourceMode mode;
        public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
        public MGRenderCacheSourceMode Mode { get => mode; set { mode = value; PropertyChanged?.Invoke(this, new(nameof(Mode))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
