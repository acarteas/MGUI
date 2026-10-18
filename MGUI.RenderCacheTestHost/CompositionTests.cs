using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class CompositionTests : Game
{
    private readonly Dictionary<string, bool> Checks = new();
    private readonly List<object> Comparisons = new();
    private int Frame;
    private bool Completed;
    private string Evidence = "";
    private CompositionTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }
    internal static int RunAllNative()
    {
        using CompositionTests game = new();
        game.Run();
        Check(game.Completed, "Composition lane did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Checks));
        Console.WriteLine("ARTIFACTS:" + game.Evidence);
        return 0;
    }
    protected override void LoadContent()
    {
        Evidence = Path.Combine(Path.GetTempPath(), "mgui-composition-" + Environment.ProcessId);
        Directory.CreateDirectory(Evidence);
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 48, 56, 280, 220)
        {
            WindowStyle = WindowStyle.None, Padding = new(0), BackgroundBrush = new((IFillBrush)null!)
        };
        desktop.Windows.Add(window);
        MGRenderCache boundary = new(window) { Padding = new(9) };
        PhaseHost phases = new(window);
        boundary.SetContent(phases);
        window.SetContent(boundary);
        Color scene = Color.Transparent;
        void Update()
        {
            fixture.Update(++Frame);
            desktop.PrepareRenderCaches();
        }
        Color[] Paint(DrawSettings? settings = null, float opacity = 1, Point offset = default,
            Rectangle? clip = null, MGElement? painter = null)
        {
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(scene);
            long switches = GraphicsDevice.Metrics.TargetCount;
            long allocations = desktop.RenderCaches.Statistics.Allocations;
            using (DrawTransaction dt = new(desktop.Renderer, settings ?? DrawSettings.Default, true))
            {
                using (clip.HasValue ? dt.SetClipTargetTemporary(clip.Value, false) : null)
                {
                    MGElement element = painter ?? window;
                    element.Draw(new(new(TimeSpan.Zero, dt, opacity), element.VisualState, offset));
                }
            }
            Check(GraphicsDevice.Metrics.TargetCount == switches && desktop.RenderCaches.Statistics.Allocations == allocations,
                "Final composition switched targets or allocated.");
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            return pixels;
        }
        void Warm(DrawSettings? settings = null, Point offset = default)
        {
            boundary.IsCacheEnabled = true;
            for (int settle = 0; settle < 4; settle++) { Update(); Paint(settings, offset: offset); }
            long hits = desktop.RenderCaches.Statistics.Hits;
            Update(); Paint(settings, offset: offset);
            Check(desktop.RenderCaches.Statistics.Hits == hits + 1, "Expected warm content: " + boundary.LastBypassReason);
        }
        void Pair(string name, DrawSettings? settings = null, float opacity = 1, Point offset = default,
            Rectangle? clip = null, int rgbAllowance = 0)
        {
            Color[] cached = Paint(settings, opacity, offset, clip);
            boundary.IsCacheEnabled = false;
            Compare(name, Paint(settings, opacity, offset, clip), cached, rgbAllowance);
        }
        void Fallback(string name, MGRenderCacheBypassReason reason, DrawSettings? settings = null,
            float opacity = 1, Rectangle? clip = null)
        {
            long hits = desktop.RenderCaches.Statistics.Hits;
            Color[] fallback = Paint(settings, opacity, clip: clip);
            Check(boundary.LastBypassReason == reason, name + ": expected " + reason + ", got " + boundary.LastBypassReason);
            Check(desktop.RenderCaches.Statistics.Hits == hits && desktop.RenderCaches.PendingCount == 0,
                name + " reused/queued before reference toggle.");
            boundary.IsCacheEnabled = false;
            Compare(name, Paint(settings, opacity, clip: clip), fallback);
        }

        Warm();
        Pair("four-component-phases-background-self-content-overlay");
        Checks["component-painter-order"] = true;

        // Every phase must have an observable footprint, so equality cannot hide omitted stages.
        boundary.IsCacheEnabled = false;
        Color[] allLayers = Paint();
        foreach (var layer in phases.Layers)
        {
            layer.Visibility = Visibility.Hidden;
            Color[] omitted = Paint();
            Check(allLayers.Zip(omitted).Count(pair => pair.First != pair.Second) > 30, "Invisible phase omission control.");
            layer.Visibility = Visibility.Visible;
        }
        Checks["phase-omission-controls"] = true;

        MGOverlayPanel siblings = new(window);
        window.SetContent((MGElement)null!);
        siblings.TryAddChild(new MGRectangle(window, 280, 220, Color.Transparent, 0, Color.Navy));
        siblings.TryAddChild(boundary);
        siblings.TryAddChild(new MGRectangle(window, 35, 35, Color.Transparent, 0, Color.Yellow)
            { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
        window.SetContent(siblings);
        boundary.BackgroundBrush = new(new MGSolidFillBrush(Color.DarkGreen));
        boundary.OverlayBrush = new MGPaddedFillBrush(new MGSolidFillBrush(Color.White * 0.25f), new(65));
        phases.BackgroundBrush = new(new MGSolidFillBrush(Color.DarkSlateBlue));
        Warm(); Pair("ordinary-siblings-wrapper-background-overlay");
        Checks["wrapper-and-sibling-painter-order"] = true;
        window.SetContent((MGElement)null!);
        siblings.TryRemoveChild(boundary);
        window.SetContent(boundary);
        boundary.BackgroundBrush = new((IFillBrush)null!);
        boundary.OverlayBrush = null;
        phases.BackgroundBrush = new(new MGSolidFillBrush(Color.DarkSlateBlue * 0.5f));

        MGOverlayPanel alpha = new(window);
        MGRectangle red = new(window, 200, 160, Color.Transparent, 0, Color.Red) { Opacity = 0.5f };
        MGRectangle blue = new(window, 200, 160, Color.Transparent, 0, Color.Blue) { Opacity = 0.5f };
        alpha.TryAddChild(red, new(0, 0, 45, 35));
        alpha.TryAddChild(blue, new(30, 25, 0, 0));
        boundary.SetContent(alpha);
        Warm(); Pair("descendant-alpha-transparent-scene");
        Color[] fullAlpha = Paint();
        foreach (MGRectangle layer in new[] { red, blue })
        {
            layer.Visibility = Visibility.Hidden;
            Check(fullAlpha.Zip(Paint()).Count(pair => pair.First != pair.Second) > 1000, "Translucent layer omission was invisible.");
            layer.Visibility = Visibility.Visible;
        }
        scene = new(19, 37, 53);
        Warm(); Pair("descendant-alpha-opaque-scene", rgbAllowance: 1);
        Warm(); Fallback("incoming-opacity-is-per-primitive", MGRenderCacheBypassReason.Opacity, opacity: 0.25f);
        Warm(); boundary.Opacity = 0.5f;
        Fallback("boundary-opacity-is-per-primitive", MGRenderCacheBypassReason.Opacity);
        boundary.Opacity = 1;
        Warm(); window.Opacity = 0.5f;
        Fallback("ancestor-opacity-is-per-primitive", MGRenderCacheBypassReason.Opacity);
        window.Opacity = 1;
        scene = Color.Transparent;
        Checks["per-primitive-descendant-and-inherited-alpha"] = true;

        boundary.SetContent(phases);
        using BasicEffect effect = new(GraphicsDevice) { TextureEnabled = true, VertexColorEnabled = true,
            World = Matrix.Identity, View = Matrix.Identity, Projection = Matrix.CreateOrthographicOffCenter(0, 640, 480, 0, 0, 1) };
        Matrix shear = Matrix.Identity; shear.M21 = 0.125f;
        foreach (var item in new (string Name, DrawSettings Settings, MGRenderCacheBypassReason Reason)[]
        {
            ("scale", new(Matrix.CreateScale(1.125f)), MGRenderCacheBypassReason.Transform),
            ("rotation", new(Matrix.CreateRotationZ(0.04f)), MGRenderCacheBypassReason.Transform),
            ("shear", new(shear), MGRenderCacheBypassReason.Transform),
            ("fractional-translation", new(Matrix.CreateTranslation(0.5f, 0.25f, 0)), MGRenderCacheBypassReason.Transform),
            ("additive", DrawSettings.Default with { BlendType = BlendType.Additive }, MGRenderCacheBypassReason.Blend),
            ("opaque", DrawSettings.Default with { BlendType = BlendType.Opaque }, MGRenderCacheBypassReason.Blend),
            ("nonpremultiplied", DrawSettings.Default with { BlendType = BlendType.NonPremultiplied }, MGRenderCacheBypassReason.Blend),
            ("linear", DrawSettings.Default with { SamplerType = SamplerType.LinearClamp }, MGRenderCacheBypassReason.Sampler),
            ("point-wrap", DrawSettings.Default with { SamplerType = SamplerType.PointWrap }, MGRenderCacheBypassReason.Sampler),
            ("depth", DrawSettings.Default with { DepthStencilType = DepthStencilType.DepthRead }, MGRenderCacheBypassReason.Depth),
            ("immediate", DrawSettings.Default with { Sort = SpriteSortMode.Immediate }, MGRenderCacheBypassReason.SortMode),
            ("no-scissor", DrawSettings.Default with { RasterizerType = RasterizerType.Solid }, MGRenderCacheBypassReason.Rasterizer),
            ("wireframe", DrawSettings.Default with { RasterizerType = RasterizerType.WireframeScissorTest }, MGRenderCacheBypassReason.Rasterizer),
            ("caller-effect", DrawSettings.Default with { Effect = effect }, MGRenderCacheBypassReason.Effect)
        })
        {
            Warm(); Fallback(item.Name, item.Reason, item.Settings);
        }
        Warm(); window.Scale = 1.25f;
        Fallback("window-scale", MGRenderCacheBypassReason.Transform);
        window.Scale = 1;
        Warm();
        GraphicsDevice.Viewport = new Viewport(0, 0, 640, 400);
        Fallback("noncanonical-viewport", MGRenderCacheBypassReason.Viewport);
        GraphicsDevice.Viewport = new Viewport(0, 0, 640, 480);
        Warm(); phases.RenderScale = new ConditionalScaleTransform(1.2f, 1.2f);
        phases.IsRenderVolatile = true;
        Fallback("declared-volatility", MGRenderCacheBypassReason.Volatile);
        phases.IsRenderVolatile = false; phases.RenderScale = null;
        Warm();
        phases.RenderScale = new ConditionalScaleTransform(1.2f, 1.2f);

        // Use the normal state setter for a deterministic hover, as in the native interactive fixture.
        typeof(MGElement).GetProperty(nameof(MGElement.VisualState))!.SetValue(phases,
            new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.Hovered));
        Fallback("active-descendant-render-scale", MGRenderCacheBypassReason.Transform);
        phases.RenderScale = null;
        boundary.SetContent(new UnknownHost(window));
        boundary.IsCacheEnabled = true;
        for (int settle = 0; settle < 3; settle++) { Update(); }
        Fallback("unknown-custom-renderer", MGRenderCacheBypassReason.UnsupportedRenderer);
        boundary.SetContent(phases);
        using (DrawTransaction dt = new(desktop.Renderer, DrawSettings.Default, true))
        {
            foreach (float shift in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
            {
                using (dt.SetTransformTemporary(Matrix.CreateTranslation(shift, 0, 0)))
                {
                    var args = new ElementDrawArgs(new(TimeSpan.Zero, dt, 1), boundary.VisualState, Point.Zero);
                    Check(MGRenderCacheOperation.GetContextBypassReason(args, boundary.LayoutBounds)
                        is MGRenderCacheBypassReason.Transform or MGRenderCacheBypassReason.InvalidBounds,
                        "Nonfinite/overflow translation qualified.");
                }
            }
        }
        Checks["unsupported-context-reasons-and-direct-parity"] = true;

        DrawSettings translated = new(Matrix.CreateTranslation(13, 17, 0));
        Warm(translated, new(7, 11)); Pair("integer-transform-and-nonzero-offset", translated, offset: new(7, 11));
        Warm();
        Rectangle partial = boundary.LayoutBounds; partial.Width /= 2;
        Fallback("partial-inherited-clip", MGRenderCacheBypassReason.PartialClip, clip: partial);
        Warm();
        Rectangle full = boundary.LayoutBounds; full.Inflate(5, 5);
        long fullHits = desktop.RenderCaches.Statistics.Hits;
        Pair("changed-containing-clip", clip: full);
        Check(desktop.RenderCaches.Statistics.Hits == fullHits + 1, "Containing clip unnecessarily missed.");
        Warm(); phases.ClipToBounds = false;
        Fallback("unclipped-overflow", MGRenderCacheBypassReason.UnboundedPaint);
        phases.ClipToBounds = true;
        Checks["offset-full-partial-clip-and-overflow"] = true;

        // Reproduce the exact window preview draw arguments; the main draw must restore live child observations.
        Warm();
        long previewHits = desktop.RenderCaches.Statistics.Hits;
        Paint(opacity: 0.25f, offset: new(27, 19));
        Check(boundary.LastBypassReason == MGRenderCacheBypassReason.Opacity && desktop.RenderCaches.PendingCount == 0,
            "Preview queued content.");
        int beforeRestore = phases.Paints;
        Paint();
        Check(phases.Paints == beforeRestore + 1 && desktop.RenderCaches.Statistics.Hits == previewHits,
            "Main draw reused pre-preview content observation.");
        Update();
        beforeRestore = phases.Paints; Paint();
        Check(phases.Paints == beforeRestore && desktop.RenderCaches.Statistics.Hits == previewHits + 1,
            "Main observation did not recover after direct restoration.");
        Paint(clip: new Rectangle(0, 0, 1, 1), painter: phases);
        Check(phases.RecentDrawWasClipped, "Independent clipped descendant did not execute.");
        beforeRestore = phases.Paints; Paint();
        Check(phases.Paints == beforeRestore + 1 && !phases.RecentDrawWasClipped, "Independent descendant failed to retire observation.");
        Checks["main-preview-main-and-alternate-descendant-observations"] = true;

        foreach (int stage in new[] { 0, 1, 2 })
        {
            int calls = 0;
            EventHandler<MGElement.MGElementDrawEventArgs> hook = (_, _) => calls++;
            if (stage == 0) { phases.OnBeginDraw += hook; }
            if (stage == 1) { phases.OnEndingDraw += hook; }
            if (stage == 2) { phases.OnEndDraw += hook; }
            boundary.IsCacheEnabled = true;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                int previous = calls; Update();
                Check(calls == previous, "Prepare executed public callback.");
                Paint(); Check(calls == previous + 1, "Direct callback frequency changed.");
                Check(boundary.LastBypassReason == MGRenderCacheBypassReason.PublicDrawCallback, "Callback did not diagnose bypass.");
            }
            if (stage == 0) { phases.OnBeginDraw -= hook; }
            if (stage == 1) { phases.OnEndingDraw -= hook; }
            if (stage == 2) { phases.OnEndDraw -= hook; }
            Warm();
        }
        Checks["public-callbacks-never-prepare-and-recover"] = true;

        using Texture2D effectTexture = new(GraphicsDevice, 8, 8);
        effectTexture.SetData(Enumerable.Repeat(Color.White, 64).ToArray());
        MGImage effectImage = new(window, effectTexture) { Effect = effect };
        int configurations = 0;
        effectImage.ConfigureEffect = (shader, args, _, _) =>
        {
            configurations++;
            ((BasicEffect)shader).DiffuseColor = new Vector3((args.Offset.X + 128) / 256f, 0.5f, 0.25f);
        };
        boundary.SetContent(effectImage);
        boundary.IsCacheEnabled = true;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            int previous = configurations; Update();
            Check(configurations == previous, "Prepare ran position-dependent effect callback.");
            Paint();
            Check(configurations == previous + 1 && boundary.LastBypassReason == MGRenderCacheBypassReason.UnsupportedRenderer,
                "Untracked image effect did not remain live.");
        }
        Fallback("position-dependent-image-effect", MGRenderCacheBypassReason.UnsupportedRenderer);
        Check(!Paint(offset: new(20, 0)).SequenceEqual(Paint()), "Position-dependent effect control did not change pixels.");
        Checks["position-dependent-effects-stay-live"] = true;
        boundary.SetContent(phases);

        // Exercise the public desktop entry before its own clip normalizes the caller settings.
        MGCachedFillBrush brush = new(new MGSolidFillBrush(Color.DarkRed));
        boundary.BackgroundBrush = new(brush);
        boundary.IsCacheEnabled = true;
        for (int settle = 0; settle < 5; settle++) { Update(); Paint(); }
        long bothHits = desktop.RenderCaches.Statistics.Hits;
        Update(); Paint();
        Check(desktop.RenderCaches.Statistics.Hits == bothHits + 2, "Panel/background did not independently warm.");
        long unsupportedHits = desktop.RenderCaches.Statistics.Hits;
        using (DrawTransaction dt = new(desktop.Renderer, DrawSettings.Default with { RasterizerType = RasterizerType.Solid }, true))
        {
            desktop.Draw(dt);
        }
        Check(desktop.RenderCaches.Statistics.Hits == unsupportedHits && desktop.RenderCaches.PendingCount == 0
            && boundary.LastBypassReason == MGRenderCacheBypassReason.Rasterizer,
            "Desktop normalized unsupported rasterizer into a panel/brush hit.");
        boundary.BackgroundBrush = new((IFillBrush)null!);
        Checks["desktop-rasterizer-provenance-including-brush"] = true;

        boundary.BackgroundBrush = new(new MGCachedFillBrush(new MGSolidFillBrush(Color.DarkRed)));
        void WarmPanelAndBrush(float opacity = 1)
        {
            boundary.IsCacheEnabled = true;
            for (int settle = 0; settle < 5; settle++) { Update(); Paint(opacity: opacity); }
            long hits = desktop.RenderCaches.Statistics.Hits;
            Update(); Paint(opacity: opacity);
            Check(desktop.RenderCaches.Statistics.Hits == hits + 2, "Callback scenario did not warm panel and brush.");
        }
        foreach (MGElement callbackOwner in new MGElement[] { boundary, window })
        {
            bool changeRasterizer = false;
            int calls = 0;
            EventHandler<MGElement.MGElementDrawEventArgs> changeSettings = (_, args) =>
            {
                calls++;
                if (changeRasterizer)
                {
                    args.DA.DT.SetDrawSettings(args.DA.DT.CurrentSettings with { RasterizerType = RasterizerType.Solid });
                }
            };
            callbackOwner.OnBeginDraw += changeSettings;
            WarmPanelAndBrush();
            changeRasterizer = true;
            int beforePrepareCalls = calls;
            Update();
            Check(calls == beforePrepareCalls, "Preparation invoked a live outside-content callback.");
            Fallback(callbackOwner == boundary ? "wrapper-begin-rasterizer" : "ancestor-begin-rasterizer",
                MGRenderCacheBypassReason.Rasterizer);
            Check(calls == beforePrepareCalls + 2, "Callback did not run exactly once in each normal/reference draw.");
            window.Opacity = 0.5f;
            changeRasterizer = false;
            WarmPanelAndBrush(opacity: 2);
            changeRasterizer = true;
            Fallback(callbackOwner == boundary ? "wrapper-rasterizer-through-opacity-copy" : "ancestor-rasterizer-through-opacity-copy",
                MGRenderCacheBypassReason.Rasterizer, opacity: 2);
            boundary.IsCacheEnabled = true;
            Fallback(callbackOwner == boundary ? "wrapper-rasterizer-half-opacity" : "ancestor-rasterizer-half-opacity",
                MGRenderCacheBypassReason.Opacity, opacity: 1);
            window.Opacity = 1;
            callbackOwner.OnBeginDraw -= changeSettings;
            WarmPanelAndBrush();
        }
        boundary.BackgroundBrush = new((IFillBrush)null!);
        Checks["live-begin-callback-rasterizer-provenance-and-recovery"] = true;
        Checks["rasterizer-context-survives-opacity-copies"] = true;



        MGScrollViewer scroll = new(window, ScrollBarVisibility.Visible, ScrollBarVisibility.Disabled);
        MGStackPanel rows = new(window, Orientation.Vertical) { Spacing = 3 };
        for (int row = 0; row < 12; row++)
        {
            rows.TryAddChild(new MGRectangle(window, 200, 34, Color.White, 1, row % 2 == 0 ? Color.DarkRed : Color.DarkBlue));
        }
        scroll.SetContent(rows);
        boundary.SetContent(scroll);
        Warm(); scroll.VerticalOffset = 45;
        Warm(); Pair("scroll-screen-scissor-target-local-mapping");
        Warm();
        bool[] flags = rows.Children.Select(child => child.RecentDrawWasClipped).ToArray();
        scroll.InvalidateVisual(); Paint(); Update();
        Check(flags.SequenceEqual(rows.Children.Select(child => child.RecentDrawWasClipped)), "Prepare changed clipped flags.");
        Pair("scroll-capture-preserves-descendant-clipping");
        Checks["scroll-clip-mapping-and-live-bookkeeping"] = true;

        using Texture2D image = new(GraphicsDevice, 8, 8);
        image.SetData(Enumerable.Repeat(Color.Gold, 64).ToArray());
        MGStackPanel scaled = new(window, Orientation.Vertical) { Padding = new(3), Spacing = 4 };
        scaled.TryAddChild(new MGTextBlock(window, "EDGE glyphs gjpq", Color.White, 16));
        scaled.TryAddChild(new MGRectangle(window, 60, 25, Color.Coral, 2, Color.Blue));
        scaled.TryAddChild(new MGImage(window, image));
        boundary.SetContent(scaled);
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            desktop.SetUniformUIScale(scale);
            Warm(); Pair("layout-scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        desktop.SetUniformUIScale(1);
        foreach (var category in Enum.GetValues<MGScaleCategory>().Where(value => value != MGScaleCategory.None))
        {
            MGScaleSettings settings = new();
            typeof(MGScaleSettings).GetProperty(category + "Scale")!.SetValue(settings, 1.5f);
            window.UIScaleOverride = settings;
            Warm(); Pair("layout-channel-" + category);
        }
        scaled.UIScaleMode = UIScaleMode.Disabled;
        Warm(); Pair("disabled-layout-scale-inheritance");
        window.UIScaleOverride = null;
        Checks["layout-scale-channels-override-and-inheritance"] = true;

        desktop.DisposeRenderCaches();
        File.WriteAllText(Path.Combine(Evidence, "report.json"), JsonSerializer.Serialize(new
        {
            backend = "MonoGame DesktopGL", configuration = "Release", checks = Checks, comparisons = Comparisons,
            hostSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CompositionTests).Assembly.Location))),
            coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MGRenderCache).Assembly.Location)))
        }, new JsonSerializerOptions { WriteIndented = true }));
        Completed = true;
        Exit();
    }
    private void Compare(string name, Color[] direct, Color[] cached, int rgbAllowance = 0)
    {
        int[] maxima = new int[4];
        int different = 0;
        int[] channelCounts = new int[4];
        int firstDifference = -1;
        Color[] differenceImage = new Color[direct.Length];
        for (int index = 0; index < direct.Length; index++)
        {
            if (direct[index] != cached[index])
            {
                different++;
                if (firstDifference < 0) { firstDifference = index; }
            }
            int[] a = [direct[index].R, direct[index].G, direct[index].B, direct[index].A];
            int[] b = [cached[index].R, cached[index].G, cached[index].B, cached[index].A];
            for (int channel = 0; channel < 4; channel++)
            {
                int error = Math.Abs(a[channel] - b[channel]);
                maxima[channel] = Math.Max(maxima[channel], error);
                if (error != 0) { channelCounts[channel]++; }
            }
            differenceImage[index] = new Color(Math.Min(255, Math.Abs(a[0] - b[0]) * 64),
                Math.Min(255, Math.Abs(a[1] - b[1]) * 64), Math.Min(255, Math.Abs(a[2] - b[2]) * 64), 255);
        }
        using Texture2D image = new(GraphicsDevice, 640, 480);
        foreach (var item in new[] { ("direct", direct), ("cached", cached), ("difference-x64", differenceImage) })
        {
            image.SetData(item.Item2);
            using FileStream file = File.Create(Path.Combine(Evidence, name + "-" + item.Item1 + ".png"));
            image.SaveAsPng(file, 640, 480);
        }
        Comparisons.Add(new { name, different, maxima, channelCounts, firstDifference, allowed = new[] { rgbAllowance, rgbAllowance, rgbAllowance, 0 } });
        Check(maxima.Take(3).All(value => value <= rgbAllowance) && maxima[3] == 0,
            name + " RGBA mismatch " + string.Join(",", maxima) + "; images " + Evidence);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private sealed class UnknownHost(MGWindow window) : MGContentPresenter(window) { }

    private sealed class PhaseHost : MGContentPresenter
    {
        internal int Paints;
        internal List<MGElement> Layers = new();
        protected override bool SupportsRenderCaching => true;
        internal PhaseHost(MGWindow window) : base(window)
        {
            BackgroundBrush = new(new MGSolidFillBrush(Color.DarkSlateBlue * 0.5f));
            OverlayBrush = new MGPaddedFillBrush(new MGSolidFillBrush(Color.Gold * 0.5f), new(50));
            foreach (var stage in Enum.GetValues<ComponentDrawPriority>())
            {
                int position = (int)stage;
                MGRectangle layer = new(window, 40, 60, Color.Transparent, 0,
                    new[] { Color.Red, Color.Green, Color.Blue, Color.Cyan }[position]);
                Layers.Add(layer);
                AddComponent(new MGComponent<MGRectangle>(layer, ComponentUpdatePriority.AfterContents, stage,
                    true, true, false, false, false, false, false,
                    (bounds, _) => new(bounds.X + position * 30, bounds.Y + position * 18, 70, 70)));
            }
            MGRectangle content = new(window, 130, 90, Color.Transparent, 0, Color.Purple)
                { Margin = new(22), Opacity = 0.5f };
            Layers.Add(content);
            SetContent(content);
        }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            Paints++;
            new MGSolidFillBrush(Color.Orange * 0.5f).Draw(args, this, new(bounds.X + 8, bounds.Y + 8, 85, 85));
        }
    }
}
