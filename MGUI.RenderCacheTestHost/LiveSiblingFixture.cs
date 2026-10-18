using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

// Reusable native consumer fixture: ordinary containers, explicit static boundaries, and live siblings.
internal sealed class LiveSiblingFixture : IRenderHost, IDisposable
{
    internal const int Width = 640;
    internal const int Height = 480;
    internal static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(20);
    internal static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(400);
    private readonly Game Game;
    private readonly FontSystem Font;
    private readonly MainRenderer Renderer;
    private TimeSpan? PulseStarted;
    internal TimeSpan Clock { get; private set; }
    internal MouseState Mouse { get; set; }
    internal KeyboardState Keyboard { get; set; }
    internal float Scale { get; }
    internal MGDesktop Desktop { get; }
    internal MGWindow Window { get; }
    internal MGOverlayPanel Root { get; }
    internal MGRenderCache StaticBoundary { get; }
    internal MGRenderCache FrontBoundary { get; }
    internal MGRenderCache InteractiveBoundary { get; }
    internal PaintCounter StaticArt { get; }
    internal PaintCounter FrontArt { get; }
    internal PaintCounter InteractiveArt { get; }
    internal MGTextBlock Score { get; }
    internal MGButton Button { get; }
    internal MGToggleButton Toggle { get; }
    internal MGSlider Slider { get; }
    internal MGTextBox TextBox { get; }
    internal MGScrollViewer Scroll { get; }
    internal MGButton InsideButton { get; }
    internal MGContextMenu Menu { get; }
    internal MGToolTip Tip { get; }
    internal List<MGButton> Rows { get; } = new();
    internal List<string> Events { get; } = new();
    internal int LivePaints { get; private set; }
    internal int LiveUpdates { get; private set; }
    internal bool IsPulsing => PulseStarted.HasValue && Clock - PulseStarted.Value < PulseDuration;
    internal IEnumerable<MGRenderCache> Boundaries => new[] { StaticBoundary, FrontBoundary, InteractiveBoundary };
    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;

    internal LiveSiblingFixture(Game game, float scale, int painterOrder, bool cached)
    {
        Game = game;
        Scale = scale;
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        Font = new();
        Font.AddFont(fontBytes);
        FontStashSharpTextEngine textEngine = new();
        textEngine.AddFontSystem("NotoSans", CustomFontStyles.Normal, Font, fontBytes);
        Renderer = new(this, new FontManager("NotoSans")) { TextEngine = textEngine };
        Desktop = new(Renderer);
        Desktop.SetUniformUIScale(scale);
        Window = new(Desktop, 31, 29, 600, 440)
        {
            WindowStyle = WindowStyle.None, Padding = new(0), BackgroundBrush = new((IFillBrush)null!)
        };
        Desktop.Windows.Add(Window);
        Root = new(Window) { PreferredWidth = 390, PreferredHeight = 280,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        Window.SetContent(Root);
        StaticArt = new(Window);
        MGOverlayPanel art = new(Window);
        AddAt(art, new MGBorder(Window, new(3), new MGSolidFillBrush(Color.SlateBlue)), 0, 0, 380, 270);
        AddAt(art, new MGRectangle(Window, 28, 60, Color.Transparent, 0, Color.Navy), 67, 12, 28, 60);
        AddAt(art, new MGTextBlock(Window, "STATIC FRAME / LABELS", Color.LightGray, 16), 15, 245, 250, 26);
        StaticArt.SetContent(art);
        StaticBoundary = Boundary(StaticArt, cached, hitTest: false);
        FrontArt = new(Window);
        MGOverlayPanel front = new(Window);
        AddAt(front, new MGRectangle(Window, 24, 60, Color.Transparent, 0, Color.Maroon), 132, 12, 24, 60);
        FrontArt.SetContent(front);
        FrontBoundary = Boundary(FrontArt, cached, hitTest: false);
        Score = new(Window, "SCORE 001200", Color.Gold, 20) { WrapText = false, IsRenderVolatile = true };
        Score.OnEndDraw += (_, _) => LivePaints++;
        Score.OnEndUpdate += (_, _) => LiveUpdates++;
        if (painterOrder == 0)
        {
            AddAt(Root, Score, 20, 20, 210, 45);
        }
        Root.TryAddChild(StaticBoundary);
        if (painterOrder == 1)
        {
            AddAt(Root, Score, 20, 20, 210, 45);
        }
        Root.TryAddChild(FrontBoundary);
        if (painterOrder == 2)
        {
            AddAt(Root, Score, 20, 20, 210, 45);
        }
        Button = new(Window) { PressedContentOffset = new(2, 2) };
        Button.SetContent(new MGTextBlock(Window, "LIVE", Color.White, 14));
        Button.OnLeftClicked += (_, _) => { Events.Add("button"); Button.IsSelected = !Button.IsSelected; };
        AddAt(Root, Button, 20, 80, 110, 30);
        Toggle = new(Window);
        Toggle.SetContent(new MGTextBlock(Window, "ON", Color.White, 14));
        AddAt(Root, Toggle, 140, 80, 70, 30);
        Slider = new(Window, 0, 100, 20) { Padding = new(0), ThumbWidth = 14, ThumbHeight = 22 };
        Slider.ValueChanged += (_, _) => Events.Add("slider:" + Slider.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        AddAt(Root, Slider, 20, 125, 180, 30);
        TextBox = new(Window) { Text = "", Padding = new(3) };
        AddAt(Root, TextBox, 20, 170, 180, 40);
        Scroll = new(Window, ScrollBarVisibility.Visible, ScrollBarVisibility.Disabled);
        MGStackPanel rows = new(Window, Orientation.Vertical) { Spacing = 2 };
        for (int index = 0; index < 12; index++)
        {
            int rowIndex = index;
            MGButton row = new(Window) { PreferredHeight = 25 };
            row.SetContent(new MGTextBlock(Window, "ROW " + index, Color.White, 12));
            row.OnLeftClicked += (_, _) => Events.Add("row:" + rowIndex);
            rows.TryAddChild(row);
            Rows.Add(row);
        }
        Scroll.SetContent(rows);
        AddAt(Root, Scroll, 220, 80, 150, 120);
        InsideButton = new(Window) { PressedContentOffset = new(1, 2) };
        InsideButton.SetContent(new MGTextBlock(Window, "CACHED", Color.White, 14));
        InsideButton.OnLeftClicked += (_, _) => Events.Add("inside-button");
        InteractiveArt = new(Window);
        InteractiveArt.SetContent(InsideButton);
        InteractiveBoundary = Boundary(InteractiveArt, cached, hitTest: true);
        AddAt(Root, InteractiveBoundary, 220, 220, 150, 35);
        Tip = new(Window, Button, 160, 70) { ShowDelayOverride = TimeSpan.FromMilliseconds(60) };
        Tip.SetContent(new MGTextBlock(Tip, "TOOLTIP", Color.White, 14));
        Button.ToolTip = Tip;
        Menu = new(Window, "");
        Menu.AddButton("LIVE MENU ACTION", _ => Events.Add("menu"));
        Button.ContextMenu = Menu;
    }

    private MGRenderCache Boundary(MGElement content, bool enabled, bool hitTest)
    {
        MGRenderCache result = new(Window) { IsCacheEnabled = enabled, IsHitTestVisible = hitTest,
            BackgroundBrush = new((IFillBrush)null!) };
        result.SetContent(content);
        return result;
    }

    private static void AddAt(MGOverlayPanel panel, MGElement element, int left, int top, int width, int height)
    {
        element.PreferredWidth = width;
        element.PreferredHeight = height;
        element.HorizontalAlignment = HorizontalAlignment.Left;
        element.VerticalAlignment = VerticalAlignment.Top;
        panel.TryAddChild(element, new(left, top, 0, 0));
    }

    internal Point Center(MGElement element) => element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, element.LayoutBounds.Center);
    internal Point SliderPosition(float fraction)
    {
        Rectangle bounds = Slider.LayoutBounds;
        int inset = (int)(7 * Scale);
        return Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
            new Point(bounds.Left + inset + (int)((bounds.Width - 2 * inset) * fraction), bounds.Center.Y));
    }

    internal void TriggerScore()
    {
        Score.Text = "SCORE 001201";
        PulseStarted = Clock;
    }

    internal void Tick(bool prepare = true)
    {
        Clock += FrameDuration;
        if (PulseStarted.HasValue)
        {
            double progress = (Clock - PulseStarted.Value).TotalMilliseconds / PulseDuration.TotalMilliseconds;
            float pulse = progress < 1 ? (float)Math.Sin(Math.PI * Math.Clamp(progress, 0, 1)) : 0;
            Score.Foreground = new(Color.Lerp(Color.Gold, Color.White, pulse));
            Score.RenderScale = pulse > 0 ? new(1 + pulse * 0.18f, 1 + pulse * 0.18f) : null;
        }
        PreviewUpdate?.Invoke(this, Clock);
        Desktop.Update();
        EndUpdate?.Invoke(this, EventArgs.Empty);
        if (prepare)
        {
            Desktop.PrepareRenderCaches();
        }
    }

    internal Color[] Paint()
    {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
        GraphicsDevice.Clear(new Color(19, 37, 53));
        long switches = GraphicsDevice.Metrics.TargetCount;
        long allocations = Desktop.RenderCaches.Statistics.Allocations;
        Desktop.Draw();
        if (GraphicsDevice.Metrics.TargetCount != switches || Desktop.RenderCaches.Statistics.Allocations != allocations)
        {
            throw new InvalidOperationException("Final live-sibling composition allocated or switched targets.");
        }
        Color[] pixels = new Color[Width * Height];
        GraphicsDevice.GetBackBufferData(pixels);
        return pixels;
    }

    internal MGRectangle UseSizeDrivenLayout()
    {
        Window.SetContent((MGElement)null!);
        Root.TryRemoveChild(StaticBoundary);
        MGStackPanel layout = new(Window, Orientation.Horizontal) { HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, Spacing = 4 };
        MGRectangle leading = new(Window, 20, 150, Color.White, 1, Color.DarkGreen);
        StaticBoundary.PreferredWidth = 220;
        StaticBoundary.PreferredHeight = 250;
        layout.TryAddChild(leading);
        layout.TryAddChild(StaticBoundary);
        Window.SetContent(layout);
        return leading;
    }

    public Rectangle GetBounds() => new(0, 0, Width, Height);
    public MouseState GetMouseState() => Mouse;
    public KeyboardState GetKeyboardState() => Keyboard;
    public object? GetService(Type serviceType) => Game.Services.GetService(serviceType);
    public void Dispose()
    {
        Desktop.DisposeRenderCaches();
        if (Desktop.RenderCaches.EntryCount != 0 || Desktop.RenderCaches.RetainedBytes != 0
            || Desktop.RenderCaches.Statistics.Allocations != Desktop.RenderCaches.Statistics.Disposals)
        {
            throw new InvalidOperationException("Live-sibling fixture leaked cache resources.");
        }
        Renderer.SpriteBatch.Dispose();
        Renderer.PrimitiveBatch.Dispose();
        Renderer.Content.Dispose();
        Font.Dispose();
    }

    internal sealed class PaintCounter : MGContentPresenter
    {
        internal int Paints;
        internal int Captures;
        internal int Updates;
        protected override bool SupportsRenderCaching => true;
        internal PaintCounter(MGWindow window) : base(window)
        {
            OnEndUpdate += (_, _) => Updates++;
        }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            if (args.DT.IsCacheCapture)
            {
                Captures++;
            }
            else
            {
                Paints++;
            }
            base.DrawSelf(args, bounds);
        }
    }
}
