using System.Security.Cryptography;
using System.Text.Json;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Samples.Features;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

internal sealed class SampleTests : Game, IRenderHost
{
    private readonly Dictionary<string, bool> Checks = new();
    private readonly List<object> Trace = new();
    private readonly GraphicsDeviceManager Graphics;
    private MainRenderer Renderer = null!;
    private MGDesktop Desktop = null!;
    private RenderCachingSamples Sample = null!;
    private FontSystem Font = null!;
    private TimeSpan Clock;
    private MouseState Mouse;
    private string Evidence = "";
    private bool Completed;
    private int Presented;
    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;

    private SampleTests()
    {
        Graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 900,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using SampleTests game = new();
        game.Run();
        Require(game.Completed, "Sample validation did not finish.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Checks));
        Console.WriteLine("ARTIFACTS:" + game.Evidence);
        return 0;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Completed)
        {
            return;
        }

        if (Presented++ < 3)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            return;
        }

        Evidence = Path.Combine(Path.GetTempPath(), "mgui-sample-" + Environment.ProcessId);
        Directory.CreateDirectory(Evidence);
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        Font = new();
        Font.AddFont(bytes);
        FontStashSharpTextEngine text = new();
        text.AddFontSystem("NotoSans", CustomFontStyles.Normal, Font, bytes);
        Renderer = new(this, new FontManager("NotoSans")) { TextEngine = text };
        Desktop = new(Renderer);
        Sample = new(Content, Desktop);
        Sample.Show();
        try
        {
            Warm();
            Check("normal-no-allocation", Desktop.RenderCaches.Statistics.Allocations == 0);
            Save("normal");
            Click("BackgroundButton");
            Warm();
            Check("background-mode", Sample.Mode == "Background" && !Named<MGRenderCache>("StaticPanel").IsCacheEnabled
                && Desktop.RenderCaches.Statistics.Hits > 0);
            Save("background");
            Click("PanelButton");
            Warm();
            Check("panel-mode", Sample.Mode == "Panel" && Named<MGRenderCache>("StaticPanel").LastBypassReason == MGRenderCacheBypassReason.None);
            Save("panel");
            Click("ScoreButton");
            long builds = Desktop.RenderCaches.Statistics.Builds;
            ulong revision = Named<MGRenderCache>("StaticPanel").VisualRevision;
            ulong layoutRevision = Named<MGRenderCache>("StaticPanel").LayoutRevision;
            Rectangle staticBounds = Named<MGRenderCache>("StaticPanel").LayoutBounds;
            for (int i = 0; i < 10; i++)
            {
                Step();
            }
            Check("live-pulse-midpoint", Sample.IsPulsing && Named<MGTextBlock>("Score").FontSize >= 23);
            Save("pulse");
            for (int i = 0; i < 13; i++)
            {
                Step();
            }
            Check("pulse-completes", !Sample.IsPulsing && Named<MGTextBlock>("Score").FontSize == 20
                && Named<MGTextBlock>("Score").Text.Contains("000001"));
            Check("live-pulse-preserves-static-cache", Desktop.RenderCaches.Statistics.Builds == builds
                && Named<MGRenderCache>("StaticPanel").VisualRevision == revision
                && Named<MGRenderCache>("StaticPanel").LayoutRevision == layoutRevision
                && Named<MGRenderCache>("StaticPanel").LayoutBounds == staticBounds);
            Click("TitleButton");
            Warm();
            Check("static-title-rebuilds", Desktop.RenderCaches.Statistics.Builds > builds);
            builds = Desktop.RenderCaches.Statistics.Builds;
            string customPixels = RegionHash(Named<MGBorder>("CustomSwatch"));
            Click("InvalidateButton");
            Warm();
            Check("custom-source-rebuilds", Desktop.RenderCaches.Statistics.Builds > builds
                && RegionHash(Named<MGBorder>("CustomSwatch")) != customPixels);
            Save("custom-change");
            Click("VolatilityButton");
            Warm();
            Check("volatility-bypasses", Named<MGRenderCache>("StaticPanel").LastBypassReason == MGRenderCacheBypassReason.Volatile);
            Save("volatile");
            Click("VolatilityButton");
            Warm();
            builds = Desktop.RenderCaches.Statistics.Builds;
            Click("DirtyButton");
            Warm();
            ulong dirtyRevision = Named<MGRenderCache>("DirtyPanel").VisualRevision;
            string dirtyText = Named<MGTextBlock>("DirtyText").Text;
            string dirtyPixels = RegionHash(Named<MGTextBlock>("DirtyText"));
            Step();
            Check("every-update-workload-changes", Named<MGRenderCache>("DirtyPanel").VisualRevision > dirtyRevision
                && Named<MGTextBlock>("DirtyText").Text != dirtyText
                && RegionHash(Named<MGTextBlock>("DirtyText")) != dirtyPixels);
            Check("every-update-workload-bypasses", Named<MGRenderCache>("DirtyPanel").LastBypassReason != MGRenderCacheBypassReason.None
                || Desktop.RenderCaches.Statistics.Builds > builds + 2);
            Save("dirty");
            Click("DirtyButton");
            Warm();
            Click("ResizeButton");
            Warm();
            Check("resize-applied", Named<MGOverlayPanel>("Preview").PreferredHeight == 250);
            Save("resized");
            Click("ScaleButton");
            Warm();
            Check("scale-applied", Sample.Window.UIScaleOverride.FontScale == 1.25f);
            Save("scaled");
            for (int i = 1; i <= 8; i++)
            {
                Mouse = new(800, 500, -1200 * i, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Step();
            }
            Check("scaled-scroll-access", Named<MGScrollViewer>("SampleScroll").VerticalOffset > 0);
            Save("scaled-lower");
            for (int i = 7; i >= 0; i--)
            {
                Mouse = new(800, 500, -1200 * i, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Step();
            }
            Click("NormalButton");
            Warm();
            Check("normal-restored", Sample.Mode == "Normal" && Desktop.RenderCaches.EntryCount == 0);
            Desktop.DisposeRenderCaches();
            Check("deterministic-disposal", Desktop.RenderCaches.IsDisposed && Desktop.RenderCaches.RetainedBytes == 0);
            File.WriteAllText(Path.Combine(Evidence, "report.json"), JsonSerializer.Serialize(new
            {
                backend = "MonoGame DesktopGL", configuration = "Release", frameMs = 20, checks = Checks, trace = Trace,
                hostSha256 = Hash(typeof(SampleTests).Assembly.Location), coreSha256 = Hash(typeof(MGDesktop).Assembly.Location),
                sampleSourceSha256 = Hash(Path.Combine(AppContext.BaseDirectory, "Sample", "RenderCaching.xaml.cs")),
                sampleXamlSha256 = Hash(Path.Combine(AppContext.BaseDirectory, "Sample", "RenderCaching.xaml"))
            }, new JsonSerializerOptions { WriteIndented = true }));
            Completed = true;
        }
        finally
        {
            Desktop.DisposeRenderCaches();
            Font.Dispose();
        }
        Exit();
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private T Named<T>(string name) where T : MGElement => Sample.Window.GetElementByName<T>(name);
    private void Check(string name, bool value)
    {
        Require(value, name + " failed; " + JsonSerializer.Serialize(Desktop.RenderCaches.Statistics));
        Checks.Add(name, true);
    }
    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
    private void Warm()
    {
        for (int i = 0; i < 5; i++)
        {
            Step();
        }
    }
    private void Click(string name)
    {
        MGButton button = Named<MGButton>(name);
        Point center = button.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, button.LayoutBounds.Center);
        Mouse = new(center.X, center.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        Step();
        Mouse = new(center.X, center.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        Step();
        Mouse = new(center.X, center.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        Step();
        Mouse = default;
    }
    private void Step()
    {
        Clock += TimeSpan.FromMilliseconds(20);
        PreviewUpdate?.Invoke(this, Clock);
        Desktop.Update();
        EndUpdate?.Invoke(this, EventArgs.Empty);
        Desktop.PrepareRenderCaches();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
        GraphicsDevice.Clear(Color.CornflowerBlue);
        long switches = GraphicsDevice.Metrics.TargetCount;
        Desktop.Draw();
        Require(GraphicsDevice.Metrics.TargetCount == switches, "Sample composition switched targets.");
    }
    private string RegionHash(MGElement element)
    {
        Rectangle bounds = element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, element.LayoutBounds);
        Color[] pixels = new Color[bounds.Width * bounds.Height];
        GraphicsDevice.GetBackBufferData(bounds, pixels, 0, pixels.Length);
        return Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
    }
    private void Save(string name)
    {
        Color[] pixels = new Color[1280 * 900];
        GraphicsDevice.GetBackBufferData(pixels);
        using Texture2D texture = new(GraphicsDevice, 1280, 900);
        texture.SetData(pixels);
        using FileStream stream = File.Create(Path.Combine(Evidence, name + ".png"));
        texture.SaveAsPng(stream, 1280, 900);
        Trace.Add(new { name, clock = Clock.TotalMilliseconds, stats = Desktop.RenderCaches.Statistics,
            bytes = Desktop.RenderCaches.RetainedBytes, dirtyReason = Named<MGRenderCache>("DirtyPanel").LastBypassReason.ToString(), reason = Named<MGRenderCache>("StaticPanel").LastBypassReason.ToString() });
    }

    public Rectangle GetBounds() => GraphicsDevice.Viewport.Bounds;
    public object? GetService(Type serviceType) => Services.GetService(serviceType);
    MouseState IRenderHost.GetMouseState() => Mouse;
    KeyboardState IRenderHost.GetKeyboardState() => default;
}
