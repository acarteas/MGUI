using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class InteractiveTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;

    private InteractiveTests()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640,
            PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false
        };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using InteractiveTests game = new();
        game.Run();
        Check(game.Completed, "Interactive checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; native RGBA readback; renderer recorded by test-native.py.");
        return 0;
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 50, 240, 180) { WindowStyle = WindowStyle.None, Padding = new(0) };
        desktop.Windows.Add(window);
        using RenderTarget2D target = new(GraphicsDevice, 640, 480);

        void Arrange(MGElement element)
        {
            window.SetContent(element);
            for (int settle = 0; settle < 3; settle++)
            {
                fixture.Update(++Frame);
            }
        }
        Color[] Paint(MGElement element, bool capture)
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.ScissorRectangle = target.Bounds;
            GraphicsDevice.Clear(Color.Transparent);
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                if (capture)
                {
                    transaction.ConfigureCacheCapture(Point.Zero);
                }
                element.Draw(new(new(TimeSpan.Zero, transaction, 1), element.VisualState, Point.Zero));
            }
            GraphicsDevice.SetRenderTarget(null);
            Color[] pixels = new Color[640 * 480];
            target.GetData(pixels);
            return pixels;
        }
        void Parity(string name, MGElement element)
        {
            Check(Paint(element, false).SequenceEqual(Paint(element, true)), name + " capture changed pixels.");
            Results[name] = true;
        }

        MGButton button = new(window) { PressedContentOffset = new(2, 3) };
        button.SetContent(new MGRectangle(window, 20, 20, Color.Black, 1, Color.Red));
        Arrange(button);
        typeof(MGElement).GetProperty(nameof(MGElement.VisualState))!.SetValue(button, new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.Pressed));
        Parity("button-pressed-offset", button);
        MGToggleButton toggle = new(window);
        Arrange(toggle);
        toggle.IsChecked = true;
        Parity("toggle-checked", toggle);

        MGCheckBox check = new(window) { IsCheckMarkShadowed = true, CheckMarkShadowOffset = new(1, 2) };
        Arrange(check);
        Color[] uncheckedPixels = Paint(check, false);
        check.IsChecked = true;
        Color[] checkedPixels = Paint(check, false);
        Check(!uncheckedPixels.SequenceEqual(checkedPixels), "Checked mark did not paint.");
        int publicHooks = 0;
        check.ButtonElement.OnEndingDraw += (_, _) => publicHooks++;
        Parity("checkbox-checked-ending-stage", check);
        Check(publicHooks == 1, "Capture invoked public ending callback.");
        check.IsThreeState = true;
        check.IsChecked = null;
        Check(!checkedPixels.SequenceEqual(Paint(check, false)), "Indeterminate mark did not change.");
        Parity("checkbox-indeterminate-ending-stage", check);

        MGRadioButton radio = new(window, "native-radio");
        Arrange(radio);
        radio.IsChecked = true;
        Parity("radio-checked", radio);
        Color[] radioNormal = Paint(radio, false);
        radio.SetBubbleVisualState(SecondaryVisualState.Hovered);
        Check(!radioNormal.SequenceEqual(Paint(radio, false)), "Radio hover did not paint.");
        Parity("radio-hover", radio);
        radio.SetBubbleVisualState(SecondaryVisualState.Pressed);
        Parity("radio-pressed", radio);
        Color[] pressedRadio = Paint(radio, false);
        MGWindow modal = new(window, 300, 300, 100, 80);
        window.ModalWindow = modal;
        Check(radioNormal.SequenceEqual(Paint(radio, false)), "Late modal assignment did not immediately suppress radio overlay.");
        Check(radioNormal.SequenceEqual(Paint(radio, true)), "Capture retained radio overlay after late modal assignment.");
        window.ModalWindow = null;
        Check(pressedRadio.SequenceEqual(Paint(radio, false)), "Late modal removal did not restore maintained radio feedback.");
        Results["radio-late-modal-transition"] = true;

        MGSlider slider = new(window, 0, 100, 20);
        Arrange(slider);
        Color[] sliderBefore = Paint(slider, false);
        slider.Value = 70;
        Check(!sliderBefore.SequenceEqual(Paint(slider, false)), "Slider value did not move thumb.");
        Parity("slider-value", slider);
        MGProgressBar progress = new(window, 0, 100, 25);
        Arrange(progress);
        progress.Value = 75;
        Parity("progress-value", progress);
        MGProgressButton progressButton = new(window, new(1), MGUniformBorderBrush.Black) { Value = 65 };
        Arrange(progressButton);
        Parity("progress-button", progressButton);
        MGRatingControl rating = new(window, UseDiscreteValues: false);
        Arrange(rating);
        rating.SetValue(2.5f);
        Parity("rating-half-value", rating);
        MGGridColorPicker picker = new(window, 2, Color.Red, Color.Blue) { ShowSelectedColorLabel = true, SelectedColor = Color.Blue };
        Arrange(picker);
        Parity("picker-preview", picker);
        ((List<Color>)picker.Colors)[1] = Color.Yellow;
        picker.InvalidateVisual();
        Parity("picker-raw-paint-refresh", picker);
        MGTimer timer = new(window, TimeSpan.FromSeconds(30));
        Arrange(timer);
        timer.RemainingDuration = TimeSpan.FromSeconds(20);
        Parity("timer-value", timer);
        MGStopwatch stopwatch = new(window) { Elapsed = TimeSpan.FromSeconds(10) };
        Arrange(stopwatch);
        Parity("stopwatch-value", stopwatch);
        VerifyAncestorRawBrushInvalidation(fixture);
        Results["ancestor-invalidates-child-cached-brush"] = true;
        Completed = true;
        desktop.DisposeRenderCaches();
        Exit();
    }

    private void VerifyAncestorRawBrushInvalidation(BenchmarkFixture fixture)
    {
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        desktop.RenderCaches.Clear();
        MGWindow window = new(desktop, 40, 50, 240, 180) { WindowStyle = WindowStyle.None, Padding = new(0) };
        MGButton ancestor = new(window);
        MGCompositedFillBrush source = new(new MGSolidFillBrush(Color.Blue));
        MGCachedFillBrush cached = new(source) { IsCacheEnabled = false };
        MGRectangle child = new(window, 120, 80, Color.Transparent, 0, Color.Transparent) { BackgroundBrush = new(cached) };
        ancestor.SetContent(child);
        window.SetContent(ancestor);
        desktop.Windows.Add(window);
        Color[] Draw()
        {
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(Color.Transparent);
            desktop.Draw();
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        void Update() => fixture.Update(++Frame);
        for (int settle = 0; settle < 3; settle++)
        {
            Update();
            Draw();
        }
        cached.IsCacheEnabled = true;
        Update();
        Draw();
        Update();
        desktop.PrepareRenderCaches();
        long hits = desktop.RenderCaches.Statistics.Hits;
        Color[] blue = Draw();
        Check(desktop.RenderCaches.Statistics.Hits > hits, "Child cached brush did not warm.");
        source.Brushes[0] = new MGSolidFillBrush(Color.Yellow);
        ancestor.InvalidateVisual();
        hits = desktop.RenderCaches.Statistics.Hits;
        Color[] changed = Draw();
        Check(desktop.RenderCaches.Statistics.Hits == hits, "Ancestor invalidation reused a stale nested brush target.");
        Check(!blue.SequenceEqual(changed), "Raw descendant source paint did not change.");
        Update();
        desktop.PrepareRenderCaches();
        hits = desktop.RenderCaches.Statistics.Hits;
        Color[] rebuilt = Draw();
        Check(desktop.RenderCaches.Statistics.Hits > hits && changed.SequenceEqual(rebuilt), "Next observed child generation did not rebuild with matching pixels.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
