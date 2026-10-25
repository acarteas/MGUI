using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Data_Binding;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Portable.Xaml;

namespace MGUI.RenderCacheTestHost;

internal sealed class RenderCacheXamlTests : Game
{
    private bool Completed;

    internal RenderCacheXamlTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using RenderCacheXamlTests game = new();
        game.Run();
        Check(game.Completed, "RenderCache XAML checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(new { tree = true, defaultsAndStyles = true, contentApplication = true, liveBindings = true, sanitizedSyntax = true }));
        return 0;
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        using Texture2D texture = new(GraphicsDevice, 2, 2);
        texture.SetData(new[] { Color.Red, Color.Green, Color.Blue, Color.White });
        desktop.Resources.AddTexture("CacheTile", new MGTextureData(texture));
        Settings resource = new() { Volatile = true };
        desktop.Resources.AddStaticResource("CacheSettings", resource);
        HashSet<DataBinding> previousBindings = DataBindingManager.Bindings.ToHashSet();
        try
        {
            const string xaml = """
                <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Width="400" Height="360">
                  <Window.Resources><ResourceDictionary>
                    <Style Name="Base" TargetType="RenderCache"><Setter Property="IsCacheEnabled" Value="False" /></Style>
                    <Style Name="Derived" TargetType="RenderCache" BasedOn="Base"><Setter Property="IsRenderVolatile" Value="True" /></Style>
                  </ResourceDictionary></Window.Resources>
                  <StackPanel Name="Root">
                    <RenderCache Name="Static"><StackPanel Name="Nested">
                      <TextBlock Name="Label" Text="Static text" />
                      <Image Name="Picture" SourceName="CacheTile" />
                      <Button Name="Action"><TextBlock Text="Action" /></Button>
                    </StackPanel></RenderCache>
                    <TextBlock Name="Live" Text="Live sibling" IsRenderVolatile="True" />
                    <RenderCache Name="Empty" />
                    <RenderCache Name="Styled" StyleNames="Derived" />
                    <RenderCache Name="Explicit" StyleNames="Derived" IsCacheEnabled="True" IsRenderVolatile="False" />
                    <RenderCache Name="Bound" IsCacheEnabled="{MGBinding Enabled}" IsRenderVolatile="{MGBinding Volatile}"><TextBlock /></RenderCache>
                    <RenderCache Name="Named" IsCacheEnabled="{MGBinding IsEnabled, ElementName=Action}" />
                    <TextBlock Name="ResourceBound" IsRenderVolatile="{MGBinding Volatile, ResourceName=CacheSettings}" />
                  </StackPanel>
                </Window>
                """;
            MGWindow window = XAMLParser.LoadRootWindow(desktop, xaml);
            desktop.Windows.Add(window);
            T Named<T>(string name) where T : MGElement
            {
                Check(window.TryGetElementByName(name, out MGElement result) && result is T, $"Missing {typeof(T).Name} named {name}.");
                return (T)result;
            }
            MGStackPanel root = Named<MGStackPanel>("Root");
            MGRenderCache staticCache = Named<MGRenderCache>("Static");
            MGStackPanel nested = Named<MGStackPanel>("Nested");
            MGTextBlock label = Named<MGTextBlock>("Label");
            MGImage image = Named<MGImage>("Picture");
            MGButton button = Named<MGButton>("Action");
            MGTextBlock live = Named<MGTextBlock>("Live");
            Check(ReferenceEquals(window.Content, root) && ReferenceEquals(staticCache.Parent, root)
                && ReferenceEquals(staticCache.Content, nested) && ReferenceEquals(nested.Parent, staticCache)
                && ReferenceEquals(label.Parent, nested) && ReferenceEquals(image.Parent, nested) && ReferenceEquals(button.Parent, nested)
                && ReferenceEquals(live.Parent, root), "XAML content ownership changed.");
            Check(label.Text == "Static text" && image.SourceName == "CacheTile" && button.Content is MGTextBlock,
                "Nested text/image/control content did not materialize.");
            Check(staticCache.IsCacheEnabled && !staticCache.IsRenderVolatile && !label.IsRenderVolatile && live.IsRenderVolatile,
                "Unspecified defaults or ordinary sibling volatility changed.");
            MGRenderCache empty = Named<MGRenderCache>("Empty");
            MGRenderCache styled = Named<MGRenderCache>("Styled");
            MGRenderCache explicitValues = Named<MGRenderCache>("Explicit");
            Check(empty.Content == null && empty.IsCacheEnabled && !empty.IsRenderVolatile, "Empty wrapper defaults changed.");
            Check(!styled.IsCacheEnabled && styled.IsRenderVolatile && explicitValues.IsCacheEnabled && !explicitValues.IsRenderVolatile,
                "Inherited styles or explicit values did not reach runtime.");

            RenderCache source = (RenderCache)XamlServices.Parse("""
                <RenderCache xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" IsCacheEnabled="False" IsRenderVolatile="True"><TextBlock Text="Replacement" /></RenderCache>
                """);
            MGElement originalContent = staticCache.Content;
            typeof(Element).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, new object[] { root, staticCache, false });
            Check(ReferenceEquals(originalContent, staticCache.Content) && !staticCache.IsCacheEnabled && staticCache.IsRenderVolatile,
                "IncludeContent=false replaced content or skipped flags.");
            typeof(Element).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(new RenderCache(), new object[] { root, staticCache, false });
            Check(!staticCache.IsCacheEnabled && staticCache.IsRenderVolatile, "Unspecified flags reset existing runtime values.");

            MGRenderCache bound = Named<MGRenderCache>("Bound");
            MGRenderCache named = Named<MGRenderCache>("Named");
            MGTextBlock resourceBound = Named<MGTextBlock>("ResourceBound");
            Check(DataBindingManager.Bindings.Count(binding => !previousBindings.Contains(binding)) == 4,
                "Normal XAML lifecycle did not activate all four bindings.");
            Settings first = new() { Enabled = false, Volatile = true };
            window.WindowDataContext = first;
            Check(!bound.IsCacheEnabled && bound.IsRenderVolatile && resourceBound.IsRenderVolatile, "Data context/resource values were not applied.");
            ulong revision = bound.VisualRevision;
            first.Enabled = true;
            first.Volatile = false;
            Check(bound.IsCacheEnabled && !bound.IsRenderVolatile && bound.VisualRevision > revision, "Live bound flags did not invalidate runtime.");
            revision = bound.VisualRevision;
            first.Enabled = true;
            first.Volatile = false;
            Check(bound.VisualRevision == revision, "Equal binding values invalidated runtime.");
            Settings second = new() { Enabled = false, Volatile = true };
            window.WindowDataContext = second;
            first.Enabled = true;
            first.Volatile = false;
            Check(!bound.IsCacheEnabled && bound.IsRenderVolatile, "Replacement data context retained old source.");
            button.IsEnabled = false;
            resource.Volatile = false;
            Check(!named.IsCacheEnabled && !resourceBound.IsRenderVolatile, "ElementName/ResourceName bindings did not stay live.");

            MGWindow sanitized = XAMLParser.LoadRootWindow(desktop,
                "<Window Width=\"100\" Height=\"100\"><RenderCache IsCacheEnabled=\"False\"><RenderCache.Content><TextBlock IsRenderVolatile=\"True\" Text=\"Aliased\" /></RenderCache.Content></RenderCache></Window>", true);
            Check(sanitized.Content is MGRenderCache { IsCacheEnabled: false, Content: MGTextBlock { IsRenderVolatile: true } },
                "Sanitized element/property syntax did not resolve RenderCache.");
            Completed = true;
        }
        finally
        {
            foreach (DataBinding binding in DataBindingManager.Bindings.Where(binding => !previousBindings.Contains(binding)).ToArray())
            {
                DataBindingManager.RemoveBinding(binding);
            }
            desktop.Resources.RemoveStaticResource("CacheSettings");
            desktop.Resources.RemoveTexture("CacheTile");
        }
    }

    protected override void Update(GameTime gameTime) => Exit();
    protected override void Draw(GameTime gameTime) { }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Settings : INotifyPropertyChanged
    {
        private bool enabled = true;
        private bool volatileValue;
        public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
        public bool Volatile { get => volatileValue; set { volatileValue = value; PropertyChanged?.Invoke(this, new(nameof(Volatile))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
