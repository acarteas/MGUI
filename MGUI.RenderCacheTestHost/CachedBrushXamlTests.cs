using System.ComponentModel;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Data_Binding;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class CachedBrushXamlTests : Game
{
    private bool Completed;
    internal CachedBrushXamlTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using CachedBrushXamlTests game = new();
        game.Run();
        Check(game.Completed, "Cached brush XAML checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(new { deferredBindingTransfer = true, liveSources = true, resolvers = true, independentCopy = true }));
        return 0;
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        const string xaml = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Width="200" Height="160">
              <StackPanel>
                <Rectangle Name="Source" IsEnabled="False" />
                <Rectangle Name="Target">
                  <Rectangle.Background>
                    <CachedFillBrush IsCacheEnabled="{MGBinding Enabled}" SourceMode="{MGBinding Mode}" Brush="Red" />
                  </Rectangle.Background>
                  <Rectangle.Overlay>
                    <CachedFillBrush IsCacheEnabled="{MGBinding IsEnabled, ElementName=Source}" Brush="Blue" />
                  </Rectangle.Overlay>
                  <Rectangle.SelectedBackground>
                    <CachedFillBrush SourceMode="{MGBinding Mode, ResourceName=CacheSettings}" Brush="Green" />
                  </Rectangle.SelectedBackground>
                </Rectangle>
              </StackPanel>
            </Window>
            """;
        Settings resourceSettings = new() { Mode = MGRenderCacheSourceMode.ExplicitlyStatic };
        desktop.Resources.AddStaticResource("CacheSettings", resourceSettings);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, xaml);
        desktop.Windows.Add(window);
        Check(window.TryGetElementByName("Target", out MGElement owner), "Target name was not registered.");
        Check(window.TryGetElementByName("Source", out MGElement source), "Source name was not registered.");
        MGCachedFillBrush background = (MGCachedFillBrush)owner.BackgroundBrush.NormalValue;
        MGCachedFillBrush overlay = (MGCachedFillBrush)owner.OverlayBrush;
        MGCachedFillBrush selected = (MGCachedFillBrush)owner.BackgroundBrush.SelectedValue;
        MGCachedFillBrush[] targets = [background, overlay, selected];
        try
        {
            Check(DataBindingManager.Bindings.Count(binding => targets.Contains(binding.TargetObject)) == 4,
                "XAML bindings were not transferred and activated by Element.ProcessBindings.");
            Check(!overlay.IsCacheEnabled, "ElementName source was not resolved.");
            Check(selected.SourceMode == MGRenderCacheSourceMode.ExplicitlyStatic, "ResourceName source was not resolved.");
            Check(background.TryGetElementByName("Source", out MGElement resolved) && ReferenceEquals(source, resolved), "Name resolver did not use host scope.");
            Check(ReferenceEquals(background.GetDesktop(), desktop) && ReferenceEquals(background.GetResources(), desktop.Resources), "Desktop/resource resolver did not use host.");
            Settings first = new() { Enabled = false, Mode = MGRenderCacheSourceMode.ExplicitlyStatic };
            window.WindowDataContext = first;
            Check(!background.IsCacheEnabled && background.SourceMode == first.Mode, "Window data context was not propagated.");
            ulong revision = background.VisualRevision;
            first.Enabled = true;
            Check(background.IsCacheEnabled && background.VisualRevision > revision, "Live source change did not invalidate.");
            revision = background.VisualRevision;
            first.Enabled = true;
            Check(background.VisualRevision == revision, "Equal binding value invalidated.");
            Settings second = new() { Enabled = false, Mode = MGRenderCacheSourceMode.Tracked };
            window.WindowDataContext = second;
            Check(!background.IsCacheEnabled && background.SourceMode == second.Mode, "Replacement data context was not applied.");
            first.Enabled = true;
            Check(!background.IsCacheEnabled, "Old source still updated target.");
            source.IsEnabled = true;
            resourceSettings.Mode = MGRenderCacheSourceMode.Tracked;
            Check(overlay.IsCacheEnabled && selected.SourceMode == MGRenderCacheSourceMode.Tracked, "Resolver binding did not update live.");
            MGCachedFillBrush copy = (MGCachedFillBrush)background.Copy();
            Check(copy.DataContext == null && copy.GetDesktop() == null && !DataBindingManager.Bindings.Any(binding => ReferenceEquals(binding.TargetObject, copy)), "Copy retained host/context/bindings.");
            second.Enabled = true;
            Check(!copy.IsCacheEnabled && background.IsCacheEnabled, "Copy shared live source registration.");
            Completed = true;
        }
        finally
        {
            foreach (DataBinding binding in DataBindingManager.Bindings.Where(binding => targets.Contains(binding.TargetObject)).ToArray())
            {
                DataBindingManager.RemoveBinding(binding);
            }
            desktop.Resources.RemoveStaticResource("CacheSettings");
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
        private MGRenderCacheSourceMode mode;
        public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
        public MGRenderCacheSourceMode Mode { get => mode; set { mode = value; PropertyChanged?.Invoke(this, new(nameof(Mode))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
