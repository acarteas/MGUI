using System.Reflection;
using System.Text.Json;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Text;
using MGUI.FontStashSharp;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.RenderCacheTestHost;

internal sealed class TextImageTests : Game, IRenderHost
{
    private readonly Dictionary<string, bool> Results = new();
    private MGDesktop Desktop = null!;
    private MainRenderer Renderer = null!;
    private MGWindow Root = null!;
    private FontSystem Fonts = null!;
    private MouseState Mouse;
    private int Frame;
    private bool Completed;
    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;
    public Rectangle GetBounds() => new(0, 0, 640, 480);
    public MouseState GetMouseState() => Mouse;
    public KeyboardState GetKeyboardState() => default;
    public object? GetService(Type type) => Services.GetService(type);

    private TextImageTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using TextImageTests game = new();
        game.Run();
        Check(game.Completed, "Text/image checks did not finish.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; native RGBA readback and actual mouse commands/tooltips; renderer recorded by test-native.py.");
        return 0;
    }

    protected override void LoadContent()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        Fonts = new();
        Fonts.AddFont(bytes);
        FontStashSharpTextEngine engine = new();
        engine.AddFontSystem("NotoSans", CustomFontStyles.Normal, Fonts, bytes);
        Renderer = new(this, new FontManager("NotoSans")) { TextEngine = engine };
        Desktop = new(Renderer);
        Root = new(Desktop, 40, 50, 420, 300) { WindowStyle = WindowStyle.None, Padding = new(0) };
        Desktop.Windows.Add(Root);
        VerifyResourceMutationStamps();
        VerifyResourceStampEpoch();
        VerifyReentrantResourceMutation();
        VerifyText();
        VerifyRunSources();
        VerifyImages();
        VerifyTextEntry();
        VerifyInteractiveRuns();
        Desktop.DisposeRenderCaches();
        Renderer.SpriteBatch.Dispose();
        Renderer.PrimitiveBatch.Dispose();
        Renderer.Content.Dispose();
        Fonts.Dispose();
        Completed = true;
        Exit();
    }

    private void Advance(MouseState mouse = default)
    {
        Mouse = mouse;
        PreviewUpdate?.Invoke(this, TimeSpan.FromSeconds(++Frame / 60.0));
        Desktop.Update();
        EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    private void Arrange(MGElement element)
    {
        Root.SetContent(element);
        for (int settle = 0; settle < 3; settle++)
        {
            Advance();
        }
    }

    private Color[] Paint()
    {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
        GraphicsDevice.Clear(Color.Transparent);
        Desktop.Draw();
        Color[] pixels = new Color[640 * 480];
        GraphicsDevice.GetBackBufferData(pixels);
        return pixels;
    }

    private void PaintSetter(string name, MGElement element, Action change, bool pixels = true, bool changesLayout = false)
    {
        Color[] beforePixels = Paint();
        MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
        change();
        Check(element.VisualRevision == before.Visual + 1, name + " did not invalidate paint.");
        Check(changesLayout ? element.LayoutRevision > before.Layout : element.LayoutRevision == before.Layout, name + " unexpected layout invalidation.");
        if (changesLayout)
        {
            Advance();
        }
        if (pixels)
        {
            Check(!beforePixels.SequenceEqual(Paint()), name + " did not change pixels.");
        }
        MGRenderRevisionSnapshot changed = element.GetRenderRevisionSnapshot();
        change();
        Check(element.IsRenderRevisionCurrent(changed), name + " equal assignment invalidated.");
        Results[name] = true;
    }

    private void VerifyResourceMutationStamps()
    {
        using Texture2D texture = new(GraphicsDevice, 2, 2);
        texture.SetData(Enumerable.Repeat(Color.White, 4).ToArray());
        foreach (bool observed in new[] { false, true })
        {
            MGDesktop desktop = new(Renderer);
            MGWindow window = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None };
            desktop.Windows.Add(window);
            MGImage image = new(window, "stamp");
            window.SetContent(image);
            if (observed)
            {
                _ = window.VisualRevision;
            }
            else
            {
                Check(!image.IsRenderSourceSubtreeTrackingEnabled, "Unobserved stamp fixture activated tracking.");
            }
            ulong RawVisual(MGElement element) => (ulong)typeof(MGElement).GetField("CurrentVisualRevision", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(element)!;
            ulong imageBefore = RawVisual(image);
            ulong windowBefore = RawVisual(window);
            desktop.Resources.AddTexture("stamp", new(texture));
            Check(RawVisual(image) == imageBefore + 1 && RawVisual(window) == windowBefore + 1,
                $"Texture Add observed={observed} image increment={RawVisual(image) - imageBefore} root increment={RawVisual(window) - windowBefore}");
            imageBefore = RawVisual(image);
            windowBefore = RawVisual(window);
            desktop.Resources.RemoveTexture("stamp");
            Check(RawVisual(image) == imageBefore + 1 && RawVisual(window) == windowBefore + 1,
                $"Texture Remove observed={observed} image increment={RawVisual(image) - imageBefore} root increment={RawVisual(window) - windowBefore}");
            Results["keyed-texture-shared-stamp-" + observed] = true;
            desktop.DisposeRenderCaches();
        }
    }

    private void VerifyResourceStampEpoch()
    {
        MGDesktop desktop = new(Renderer);
        MGWindow window = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGImage image = new(window, "epoch");
        window.SetContent(image);
        MGRenderRevisionSnapshot before = window.GetRenderRevisionSnapshot();
        object sourceState = typeof(MGResources).GetField("_RenderCacheSource", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(desktop.Resources)!;
        sourceState.GetType().GetField("Revision", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sourceState, ulong.MaxValue);
        MGRenderMutationStamp? callbackStamp = null;
        desktop.Resources.OnTextureAdded += (_, _) => callbackStamp = desktop.Resources.CurrentTextureRenderMutation;
        using Texture2D texture = new(GraphicsDevice, 2, 2);
        texture.SetData(Enumerable.Repeat(Color.White, 4).ToArray());
        desktop.Resources.AddTexture("epoch", new(texture));
        Check(!ReferenceEquals(before.Epoch, MGRenderRevisionClock.CurrentEpoch), "Resource revision rollover did not create a new epoch.");
        Check(callbackStamp.HasValue && ReferenceEquals(callbackStamp.Value.Epoch, MGRenderRevisionClock.CurrentEpoch),
            "Texture public callback received an old epoch mutation stamp.");
        Check(image.VisualRevision == 2 && window.VisualRevision == 2 && image.LayoutRevision == 2,
            "Resource rollover split catalog and image/layout mutation generations.");
        Check(desktop.Resources.VisualRevision == 1 && desktop.Resources.CurrentTextureRenderMutation == null,
            "Resource rollover revision or scope was not normalized.");
        desktop.DisposeRenderCaches();
        Results["keyed-texture-stamp-epoch-rollover"] = true;
    }

    private void VerifyReentrantResourceMutation()
    {
        MGDesktop desktop = new(Renderer);
        MGWindow window = new(desktop, 0, 0, 160, 100) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        using Texture2D texture = new(GraphicsDevice, 2, 2);
        texture.SetData(Enumerable.Repeat(Color.White, 4).ToArray());
        MGImage outer = null!;
        MGImage inner = null!;
        desktop.Resources.OnTextureAdded += (_, args) =>
        {
            if (args.Name == "outer")
            {
                outer.TextureColor = Color.Red;
                desktop.Resources.AddTexture("inner", new(texture));
            }
        };
        desktop.Resources.OnTextureRemoved += (_, args) =>
        {
            if (args.Name == "outer")
            {
                outer.TextureColor = Color.Blue;
                desktop.Resources.RemoveTexture("inner");
            }
        };
        outer = new(window, "outer");
        inner = new(window, "inner");
        MGUI.Core.UI.Containers.MGStackPanel panel = new(window, Orientation.Horizontal);
        panel.TryAddChild(outer);
        panel.TryAddChild(inner);
        window.SetContent(panel);
        ulong rootBefore = window.VisualRevision;
        ulong outerBefore = outer.VisualRevision;
        ulong innerBefore = inner.VisualRevision;
        desktop.Resources.AddTexture("outer", new(texture));
        Check(window.VisualRevision == rootBefore + 3 && outer.VisualRevision == outerBefore + 3 && inner.VisualRevision == innerBefore + 2,
            "Nested Add callbacks swallowed distinct resource/local changes or counted one resource twice.");
        Check(ReferenceEquals(outer.ActualSource?.Texture, texture) && ReferenceEquals(inner.ActualSource?.Texture, texture),
            "Reentrant Add source reconciliation was incomplete.");
        Check(desktop.Resources.CurrentTextureRenderMutation == null, "Add leaked an active resource mutation scope.");
        rootBefore = window.VisualRevision;
        outerBefore = outer.VisualRevision;
        innerBefore = inner.VisualRevision;
        desktop.Resources.RemoveTexture("outer");
        Check(window.VisualRevision == rootBefore + 3 && outer.VisualRevision == outerBefore + 3 && inner.VisualRevision == innerBefore + 2,
            "Nested Remove callbacks swallowed distinct resource/local changes or counted one resource twice.");
        Check(outer.ActualSource == null && inner.ActualSource == null && desktop.Resources.CurrentTextureRenderMutation == null,
            "Reentrant Remove source reconciliation or mutation scope was incomplete.");
        desktop.DisposeRenderCaches();
        Results["keyed-texture-reentrant-mixed-callbacks"] = true;
    }

    private void VerifyText()
    {
        MGTextBlock text = new(Root, "1111", Color.White, 18) { WrapText = false };
        Arrange(text);
        PaintSetter("equal-size-text", text, () => text.SetText("2222", true));
        PaintSetter("inline-color", text, () => text.SetText("[fg=Red]2222[/fg]", true));
        PaintSetter("underline", text, () => text.IsUnderlined = true);
        PaintSetter("shadow", text, () => text.IsShadowed = true);
        PaintSetter("shadow-color", text, () => text.ShadowColor = Color.Blue);
        PaintSetter("shadow-offset", text, () => text.ShadowOffset = new(3, 2));
        PaintSetter("text-alignment", text, () => text.TextAlignment = HorizontalAlignment.Right);
        PaintSetter("text-progress", text, () => text.TextProgress = 0.5);
        text.TextProgress = null;
        text.SetText("plain", true);
        VisualStateSetting<Color?> foreground = new(Color.Green);
        PaintSetter("foreground-replacement", text, () => text.Foreground = foreground);
        ulong before = text.VisualRevision;
        text.Foreground.NormalValue = Color.Yellow;
        Check(text.VisualRevision == before + 1, "Foreground state-slot mutation did not invalidate exactly once.");
        Results["foreground-state-slot"] = true;
        MGRenderRevisionSnapshot fontBefore = text.GetRenderRevisionSnapshot();
        Check(text.TrySetFontSize(22), "Font size change failed.");
        Check(text.LayoutRevision > fontBefore.Layout && text.VisualRevision > fontBefore.Visual, "Font metrics did not invalidate layout/paint.");
        Results["font-metrics"] = true;
        MGRenderRevisionSnapshot sameFont = text.GetRenderRevisionSnapshot();
        text.TrySetFontSize(22);
        Check(text.IsRenderRevisionCurrent(sameFont), "Equal font invalidated.");
        text.Foreground = new(null);
        before = text.VisualRevision;
        Root.DefaultTextForeground = new(Color.Pink);
        Check(text.VisualRevision > before && text.ActualForeground == Color.Pink, "Inherited foreground stale.");
        Results["inherited-foreground"] = true;
        Root.DefaultTextForeground = new(null);
        before = text.VisualRevision;
        Desktop.Theme.TextBlockFallbackForeground.GetValue(false).NormalValue = Color.Cyan;
        Check(text.VisualRevision > before && text.ActualForeground == Color.Cyan, "Theme fallback stale.");
        Results["theme-foreground"] = true;
        before = text.VisualRevision;
        Desktop.SetUniformUIScale(1.25f);
        Check(text.VisualRevision > before && text.EffectiveFontSize > text.FontSize, "Scale-driven font resolution stale.");
        Results["font-ui-scale"] = true;
        Desktop.SetUniformUIScale(1);
        before = text.VisualRevision;
        text.TextEngineOverride = Desktop.TextEngine;
        Check(text.VisualRevision > before, "Text engine replacement stale.");
        Results["font-engine-override"] = true;
        before = text.VisualRevision;
        Desktop.FontManager.DefaultFontFamily = "ChangedDefault";
        Check(text.VisualRevision > before, "Font manager notification did not reach text.");
        MGRenderRevisionSnapshot sameDefault = text.GetRenderRevisionSnapshot();
        Desktop.FontManager.DefaultFontFamily = "ChangedDefault";
        Check(text.IsRenderRevisionCurrent(sameDefault), "Equal font manager assignment invalidated.");
        Results["font-manager-notification"] = true;
        text.TextEngineOverride = null;
        FontStashSharpTextEngine replacement = new();
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "NotoSans-Regular.ttf"));
        replacement.AddFontSystem("NotoSans", CustomFontStyles.Normal, Fonts, fontBytes);
        before = text.VisualRevision;
        Desktop.TextEngine = replacement;
        Check(text.VisualRevision > before, "Desktop font engine replacement did not reach text.");
        Results["desktop-font-engine"] = true;
        int rateNotifications = 0;
        ((System.ComponentModel.INotifyPropertyChanged)text).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MGTextBlock.TextCharactersPerSecond))
            {
                rateNotifications++;
            }
        };
        text.TextCharactersPerSecond = 5;
        text.TextCharactersPerSecond = 5;
        Check(rateNotifications == 1, "Progressive rate change must notify once; equal rate must not notify.");
        Results["progressive-rate-notification-noop"] = true;
        text.IsRenderVolatile = false;
        Check(text.IsBuiltInRenderVolatile, "Progressive text can be falsely declared static.");
        before = text.VisualRevision;
        Advance();
        Check(text.VisualRevision > before && text.TextProgress > 0, "Progressive text update stopped.");
        text.TextProgress = 1;
        Check(!text.IsBuiltInRenderVolatile, "Completed progressive text stayed volatile.");
        text.TextCharactersPerSecond = null;
        Check(!text.IsBuiltInRenderVolatile, "Paused progressive text stayed volatile.");
        Results["progressive-text-volatility"] = true;
        foreach ((string Name, Action Change) Setter in new (string, Action)[]
        {
            ("text-bold", () => text.IsBold = true),
            ("text-italic", () => text.IsItalic = true),
            ("text-wrap", () => text.WrapText = true),
            ("text-line-padding", () => text.LinePadding = 5),
            ("text-min-lines", () => text.MinLines = 2),
            ("text-max-lines", () => text.MaxLines = 3),
            ("text-formatting", () => text.AllowsInlineFormatting = false),
            ("text-normal-replacement", () => text.Text = "new text")
        })
        {
            PaintSetter(Setter.Name, text, Setter.Change, false, true);
        }
        MGRenderRevisionSnapshot idle = text.GetRenderRevisionSnapshot();
        for (int cycle = 0; cycle < 8; cycle++)
        {
            Advance();
            Paint();
        }
        Check(text.IsRenderRevisionCurrent(idle), "Idle text Update/Draw churned revisions.");
        Results["idle-text-stability"] = true;
    }

    private void VerifyRunSources()
    {
        MGTextBlock text = new(Root, "[bg=Red][u]run sources[/u][/bg]", Color.White, 18) { WrapText = false };
        Arrange(text);
        MGTextRunText parsed = text.Runs.OfType<MGTextRunText>().Single();
        MGPaddedFillBrush background = new(new MGSolidFillBrush(Color.Red), new Thickness(0));
        MGPaddedFillBrush underline = new(new MGSolidFillBrush(Color.Blue), new Thickness(0));

        // The parser creates immutable color brushes. Install public custom run data only in this test.
        MGTextRunConfig settings = parsed.Settings with
        {
            Background = new(background),
            Underline = new(true, 3, 0, underline)
        };
        MGTextRunText custom = new(parsed.Text, settings, parsed.ToolTipId, parsed.ActionId);
        typeof(MGTextBlock).GetField("<Runs>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(text, new List<MGTextRun> { custom }.AsReadOnly());
        text.InvalidateVisual();
        _ = text.VisualRevision;
        foreach ((string Name, MGPaddedFillBrush Brush, Color Color) item in new[]
        {
            ("run-background-mutation", background, Color.Green),
            ("run-underline-mutation", underline, Color.Yellow)
        })
        {
            ulong rootBefore = Root.VisualRevision;
            IFillBrush replacement = new MGSolidFillBrush(item.Color);
            PaintSetter(item.Name, text, () => item.Brush.Brush = replacement);
            Check(Root.VisualRevision == rootBefore + 1, item.Name + " did not invalidate its root exactly once.");
        }
        MGCompositedFillBrush composite = new(new MGSolidFillBrush(Color.Purple));
        background.Brush = composite;
        Color[] beforePixels = Paint();
        ulong before = text.VisualRevision;
        ulong rootRevision = Root.VisualRevision;
        ulong layout = text.LayoutRevision;
        composite.Brushes[0] = new MGSolidFillBrush(Color.Orange);
        text.InvalidateVisual();
        Check(text.VisualRevision == before + 1 && Root.VisualRevision == rootRevision + 1 && text.LayoutRevision == layout,
            "Raw nested run list refresh did not preserve one visual-only generation.");
        Check(!beforePixels.SequenceEqual(Paint()), "Raw run brush list refresh did not change native pixels.");
        using Texture2D texture = new(GraphicsDevice, 2, 2);
        texture.SetData(Enumerable.Repeat(Color.White, 4).ToArray());
        composite.Brushes.Add(new MGTextureFillBrush(new(texture), Stretch.Fill));
        text.InvalidateVisual();
        Check(text.BaseRenderSourceDependencies.SupportsRenderCaching && text.BaseRenderSourceDependencies.ResourcesAreAlive,
            "Explicit run graph refresh did not register its new texture.");
        texture.Dispose();
        Check(!text.BaseRenderSourceDependencies.ResourcesAreAlive, "Run brush texture disposal did not reach owner.");
        composite.Brushes.RemoveAt(1);
        text.InvalidateVisual();
        Check(text.BaseRenderSourceDependencies.ResourcesAreAlive, "Removing disposed run resource did not restore lifetime support.");
        using Effect effect = Content.Load<Effect>("EffectParameters").Clone();
        composite.Brushes.Add(new MGEffectFillBrush(effect));
        text.InvalidateVisual();
        Check(!text.BaseRenderSourceDependencies.SupportsRenderCaching, "Raw nested run effect was accepted.");
        composite.Brushes.RemoveAt(1);
        text.InvalidateVisual();
        Check(text.BaseRenderSourceDependencies.SupportsRenderCaching, "Removing nested run effect did not restore support.");
        Results["run-raw-list-source-support-lifetime"] = true;
    }

    private void VerifyImages()
    {
        using Texture2D first = new(GraphicsDevice, 8, 4);
        first.SetData(Enumerable.Repeat(Color.White, 16).Concat(Enumerable.Repeat(Color.Blue, 16)).ToArray());
        using Texture2D second = new(GraphicsDevice, 8, 4);
        second.SetData(Enumerable.Repeat(Color.Red, 32).ToArray());
        MGImage image = new(Root, first, new Rectangle(0, 0, 8, 2));
        Arrange(image);
        PaintSetter("image-source-rectangle", image, () => image.Source = new(first, new Rectangle(0, 2, 8, 2)));
        PaintSetter("image-equal-size-source", image, () => image.Source = new(second, new Rectangle(0, 0, 8, 2)));
        PaintSetter("image-tint", image, () => image.TextureColor = Color.Blue);
        foreach (string state in new[] { "hovered", "pressed", "selected", "disabled" })
        {
            PaintSetter("image-" + state + "-tint", image, () =>
            {
                switch (state)
                {
                    case "hovered": image.HoveredTextureColor = Color.Green; break;
                    case "pressed": image.PressedTextureColor = Color.Yellow; break;
                    case "selected": image.SelectedTextureColor = Color.Pink; break;
                    case "disabled": image.DisabledTextureColor = Color.Gray; break;
                }
            }, false);
        }
        PaintSetter("image-sampler", image, () => image.SamplerType = SamplerType.LinearClamp, false);
        ulong layout = image.LayoutRevision;
        image.Source = new(second);
        Check(image.LayoutRevision > layout, "Image dimension change did not invalidate layout.");
        Results["image-source-dimensions"] = true;
        PaintSetter("image-source-opacity", image, () => image.Source = new(second, Opacity: 0.5f));
        PaintSetter("image-stretch", image, () => image.Stretch = Stretch.None, false, true);
        MGImage named = new(Root, "image");
        Desktop.Resources.AddTexture("image", new(first));
        Arrange(named);
        ulong namedBefore = named.VisualRevision;
        Desktop.Resources.RemoveTexture("image");
        Desktop.Resources.AddTexture("image", new(second));
        Check(named.VisualRevision > namedBefore && ReferenceEquals(named.ActualSource?.Texture, second), "Keyed image source stale: before=" + namedBefore + " after=" + named.VisualRevision + " actual=" + ReferenceEquals(named.ActualSource?.Texture, second) + " name=" + named.SourceName);
        Results["image-keyed-resource"] = true;
        var rawTextures = (Dictionary<string, MGTextureData>)Desktop.Resources.Textures;
        rawTextures["image"] = new(first);
        ulong rawLayout = named.LayoutRevision;
        named.InvalidateVisual();
        Check(ReferenceEquals(named.ActualSource?.Texture, first) && named.LayoutRevision == rawLayout, "Explicit raw keyed texture refresh was stale or changed layout.");
        Results["image-explicit-raw-source"] = true;
        using Effect effect = Content.Load<Effect>("EffectParameters").Clone();
        PaintSetter("image-effect", image, () => image.Effect = effect, false);
        Check(!image.BaseRenderSourceDependencies.SupportsRenderCaching, "Effect-bearing image was accepted.");
        PaintSetter("image-standard-parameters", image, () => image.UseStandardParameters = true, false);
        MGEffectParameterValue[] values = { new("CustomInt", MGEffectParameterType.Int, 4) };
        PaintSetter("image-constant-parameters", image, () => image.Parameters = values, false);
        image.Effect = null;
        image.ConfigureEffect = (_, _, _, _) => { };
        Check(!image.BaseRenderSourceDependencies.SupportsRenderCaching, "ConfigureEffect image was accepted.");
        image.ConfigureEffect = null;
        Check(image.BaseRenderSourceDependencies.SupportsRenderCaching, "Cleared effect did not restore eligibility.");
        Results["image-effect-bypass-restoration"] = true;
        using Texture2D inlineOld = new(GraphicsDevice, 4, 4);
        inlineOld.SetData(Enumerable.Repeat(Color.Red, 16).ToArray());
        using Texture2D inlineNew = new(GraphicsDevice, 4, 4);
        inlineNew.SetData(Enumerable.Repeat(Color.Green, 16).ToArray());
        Desktop.Resources.AddTexture("inline", new(inlineOld));
        MGTextBlock inline = new(Root, "[img=inline]");
        Arrange(inline);
        _ = inline.VisualRevision;
        Color[] oldPixels = Paint();
        Desktop.Resources.RemoveTexture("inline");
        Desktop.Resources.AddTexture("inline", new(inlineNew));
        inlineOld.Dispose();
        Check(inline.BaseRenderSourceDependencies.ResourcesAreAlive, "Inline image kept the replaced texture lifetime.");
        Check(!oldPixels.SequenceEqual(Paint()), "Inline texture replacement did not paint new pixels.");
        inlineNew.Dispose();
        Check(!inline.BaseRenderSourceDependencies.ResourcesAreAlive, "Inline image ignored texture disposal.");
        Results["inline-image-resource-lifetime"] = true;
    }

    private void VerifyTextEntry()
    {
        MGTextBox text = new(Root) { Text = "selection", PlaceholderText = "placeholder" };
        Arrange(text);
        PaintSetter("text-selection", text, () => text.CurrentSelection = new(0, 4));
        PaintSetter("selection-color", text, () => text.UnfocusedSelectionBackgroundColor = Color.Red);
        PaintSetter("text-entry-mode", text, () => text.TextEntryMode = TextEntryMode.Overwrite, false);
        text.Caret.MoveToOriginalCharacterIndexOrEnd(2, true);
        text.RequestFocus();
        Advance();
        text.IsRenderVolatile = false;
        Check(text.IsBuiltInRenderVolatile, "Focused caret was not intrinsically volatile.");
        PaintSetter("caret-color", text, () => text.Caret.Color = Color.Blue, false);
        PaintSetter("caret-blink-rate", text, () => text.Caret.BlinkRate = TimeSpan.FromSeconds(1), false);
        Desktop.QueuedFocusedKeyboardHandler = null;
        Advance();
        Check(!text.IsBuiltInRenderVolatile, "Unfocused caret remained volatile.");
        Results["caret-focus-volatility"] = true;
        MGPasswordBox password = new(Root) { Text = "secret" };
        Arrange(password);
        PaintSetter("password-mask", password, () => password.PasswordCharacter = '#', changesLayout: true);
        Check(password.Text.All(c => c == '#'), "Password displayed plaintext.");
    }

    private void VerifyInteractiveRuns()
    {
        int clicks = 0;
        Desktop.Resources.AddCommand("test-action", _ => clicks++);
        MGTextBlock text = new(Root, "[Action=test-action][ToolTip=test-tip]Click here[/ToolTip][/Action]", Color.White, 18) { WrapText = false };
        MGToolTip tip = new(Root, text, 60, 30);
        Root.AddNamedToolTip("test-tip", tip);
        MGCachedFillBrush background = new(new MGSolidFillBrush(Color.DarkBlue));
        text.BackgroundBrush = new(background);
        Arrange(text);
        Check(text.HasInteractiveTextRuns && !text.IsBuiltInRenderVolatile, "Interactive text classification was not maintained independently of time volatility.");
        int warmedCycles = 0;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Paint();
            Advance(Mouse);
            Desktop.PrepareRenderCaches();
            long hits = Desktop.RenderCaches.Statistics.Hits;
            Paint();
            if (Desktop.RenderCaches.Statistics.Hits > hits)
            {
                warmedCycles++;
            }
            Rectangle bounds = text.LayoutBounds;
            Point layoutPoint = new(bounds.Left + text.EffectivePadding.Left + 2, bounds.Top + text.EffectivePadding.Top + 5);
            Point screenPoint = text.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, layoutPoint);
            MouseState released = new(screenPoint.X, screenPoint.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            MouseState pressed = new(screenPoint.X, screenPoint.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Advance(released);
            Paint();
            MethodInfo getTip = typeof(MGTextBlock).GetMethod("TryGetToolTip", BindingFlags.NonPublic | BindingFlags.Instance)!;
            object?[] args = { null };
            Check((bool)getTip.Invoke(text, args)! && ReferenceEquals(args[0], tip), "Inline tooltip failed on cycle " + cycle);
            Paint();
            Advance(pressed);
            Paint();
            Advance(released);
            Check(clicks == cycle + 1, "Actual inline command failed on cycle " + cycle + "; clicks=" + clicks);
        }
        Check(warmedCycles == 3, "Interactive text background did not warm every cycle: " + warmedCycles);
        Results["inline-action-tooltip-live-cycles"] = true;
        text.SetText("[b]plain formatted text[/b]", true);
        Check(!text.HasInteractiveTextRuns, "Removing final interactive run did not restore eligibility.");
        Results["interactive-run-removal"] = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
