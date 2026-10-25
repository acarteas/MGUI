using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace MGUI.RenderCacheTestHost;

// This correctness lane leaves the accepted benchmark workload and artifacts unchanged.
internal sealed class BackgroundParityTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private readonly List<PixelDiff> Diffs = new();
    private readonly List<RenderTarget2D> Targets = new();
    private bool Completed;
    private bool ResetExercised;
    private int FailureTargetsCreated;
    private object? WarmIdleEvidence;
    private readonly List<object> Sensitivities = new();
    private readonly string EvidenceDirectory = Path.Combine(Path.GetTempPath(), "mgui-background-parity-" + Environment.ProcessId);
    private BenchmarkFixture Fixture = null!;
    private MGDesktop Desktop => Fixture.Desktop;
    private readonly List<MGRectangle> Owners = new();
    private readonly List<MGCachedFillBrush> Wrappers = new();
    private Texture2D Tiles = null!;
    private Effect Lighting = null!;
    private Effect Tint = null!;
    private MGCompositedFillBrush Layers = null!;
    private MGCachedFillBrush Cached => Wrappers[0];
    private MGPaddedFillBrush ThemeColor = null!;

    internal BackgroundParityTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using BackgroundParityTests game = new();
        try
        {
            game.Run();
            Check(game.Completed, "Background parity checks did not complete.");
            GraphicsDevice device = game.GraphicsDevice;
            game.Dispose();
            Check(device.IsDisposed && game.Targets.All(x => x.IsDisposed), "Native device teardown retained cache-owned targets.");
            game.Pass("native-device-teardown-completes");
            Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
            return 0;
        }
        finally
        {
            Directory.CreateDirectory(game.EvidenceDirectory);
            string path = Path.Combine(game.EvidenceDirectory, "report.json");
            var evidence = new
            {
                backend = "MonoGame DesktopGL", configuration =
#if DEBUG
                    "Debug",
#else
                    "Release",
#endif
                framework = typeof(Game).Assembly.FullName,
                hostAssemblySha256 = AssemblyHash(typeof(BackgroundParityTests).Assembly),
                coreAssemblySha256 = AssemblyHash(typeof(MGDesktop).Assembly),
                clock = "All host updates use fixed simulation frame0 (1/60 second); source has no time dependency",
                renderer = "Owned Xvfb glxinfo recorded by test-native.py; software correctness, no hardware FPS claim",
                deviceResetExercised = game.ResetExercised,
                deviceReset = "When exercised: real GraphicsDevice.Reset presentation reset with DeviceResetting/DeviceReset events asserted",
                resetEvidenceGap = "DesktopGL presentation reset does not simulate graphics context loss or driver/device removal",
                nativeFailure = "Injected allocator failure after genuine target construction/bind/readback; not a real device OOM",
                completed = game.Completed, comparisons = game.Diffs, layerSensitivity = game.Sensitivities,
                injectedFailureTargets = game.FailureTargetsCreated, warmIdle = game.WarmIdleEvidence, namedChecks = game.Results, targetsObserved = game.Targets.Count,
                targetsDisposed = game.Targets.Count(x => x.IsDisposed)
            };
            File.WriteAllText(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("DIFFREPORT:" + path);
            Console.WriteLine("DIFFS:" + JsonSerializer.Serialize(game.Diffs));
        }
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        Fixture = fixture;
        using Texture2D tiles = new(GraphicsDevice, 12, 12);
        using Effect lighting = Content.Load<Effect>("EffectParameters").Clone();
        using Effect tint = Content.Load<Effect>("EffectParameters").Clone();
        Tiles = tiles;
        Lighting = lighting;
        Tint = tint;
        lighting.Parameters["CustomInt"].SetValue(101);
        tint.Parameters["CustomInt"].SetValue(102);
        tint.Parameters["CustomColor"].SetValue((new Color(28, 64, 92) * 0.25f).ToVector4());
        UploadTiles(false);
        Desktop.Windows.Clear();
        ThemeColor = new MGPaddedFillBrush(new MGSolidFillBrush(Desktop.Theme.CheckMarkColor * 0.25f), new(0));

        // UV gradient lighting uses the existing compiled effect. Its standard position/time inputs are unused by mode 101.
        var localLight = new MGEffectFillBrush(lighting) { UseStandardParameters = true };
        var nineSlice = new MGNineSliceFillBrush(new(6), new MGTextureData(tiles), new(3),
            new MGCompositedFillBrush(ThemeColor, new MGSolidFillBrush(new Color(35, 65, 95) * 0.125f)));
        Layers = new MGCompositedFillBrush(localLight, nineSlice, new MGEffectFillBrush(tint) { UseStandardParameters = true });
        Wrappers.Add(new(Layers) { SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic, IsCacheEnabled = false });
        for (int i = 0; i < 2; i++)
        {
            MGWindow window = new(Desktop, 24 + 280 * i, 30 + 100 * i, 260, 230)
            {
                WindowStyle = WindowStyle.None, Padding = new(0), BackgroundBrush = new(new MGSolidFillBrush(Color.Transparent))
            };
            MGRectangle owner = new(window, 96 + 24 * i, 64 + 16 * i, Color.Transparent, 0, Color.Transparent)
            {
                BackgroundBrush = new(Cached), OverlayBrush = Cached
            };
            Owners.Add(owner);
            window.SetContent(owner);
            Desktop.Windows.Add(window);
        }
        Settle();
        try
        {
            TestPixelsAndScales();
            TestLayerSensitivity();
            TestTransparentLayers();
            TestEffectNineSlice();
            TestInvalidation();
            TestIsolationAndModes();
            TestFallbacks();
            TestWarmAndLifecycle();
        }
        finally
        {
            Desktop.DisposeRenderCaches();
        }
        Check(Targets.All(x => x.IsDisposed), "Cache target survived desktop cleanup.");
        Check(!tiles.IsDisposed && !lighting.IsDisposed && !tint.IsDisposed && !GraphicsDevice.IsDisposed,
            "Cache cleanup disposed caller resources.");
        Pass("caller-texture-effect-device-survive-desktop-disposal");
        Completed = true;
    }

    private void TestPixelsAndScales()
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            RenderTarget2D[] priorScale = LiveTargets();
            Desktop.SetUniformUIScale(scale);
            SettleEnabled();
            Check(priorScale.All(x => x.IsDisposed), "Scale replacement retained a prior target before the reference toggle.");
            CompareWarm("layered-nine-slice-local-light-scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Check(Desktop.RenderCaches.EntryCount == 4, "Two owners/two sites did not retain four independent targets at scale " + scale);
        }
        Desktop.SetUniformUIScale(1);
        Settle();
        Warm();
        RenderTarget2D[] old = LiveTargets(Owners[0]);
        Owners[0].ParentWindow.Left += 17;
        Owners[0].ParentWindow.Top += 11;
        SettleEnabled();
        Check(old.All(x => x.IsDisposed), "Translation retained changed-owner targets before the reference toggle.");
        CompareWarm("translated-window-original-local-lighting");
        old = LiveTargets(Owners[0]);
        Owners[0].PreferredWidth = 150;
        Owners[0].PreferredHeight = 106;
        Owners[0].ParentWindow.WindowWidth = 275;
        SettleEnabled();
        Check(old.All(x => x.IsDisposed), "Resize retained changed-owner targets before the reference toggle.");
        CompareWarm("resized-owner-different-dimensions");
        Pass("geometry-replacement-retires-targets-before-reference-toggle");
        Owners[1].PreferredWidth = Owners[0].PreferredWidth;
        Owners[1].PreferredHeight = Owners[0].PreferredHeight;
        Settle();
        CompareWarm("equal-size-distinct-owner-origin");
        Owners[1].IsEnabled = false;
        Settle();
        CompareWarm("shared-source-distinct-owner-visual-state");
        Owners[1].IsEnabled = true;
        Settle();
        Pass("owner-site-size-scale-translation-target-isolation");
        Warm();
        long hits = Desktop.RenderCaches.Statistics.Hits;
        var clip = new Rectangle(Owners[0].LayoutBounds.X + 4, Owners[0].LayoutBounds.Y + 5, 23, 19);
        Color[] clipped = Draw(true, clip: clip);
        Check(Desktop.RenderCaches.Statistics.Hits == hits && Desktop.RenderCaches.PendingCount == 0,
            "Partial clipped draw reused or queued cache pixels before reference toggle.");
        Compare("partial-clipped-bounds", clipped, Draw(false, clip: clip));
        Check(Desktop.RenderCaches.Statistics.Hits == hits, "Partial clip used a ready layer.");
        Pass("partial-clip-layered-direct-parity");
    }

    private void TestLayerSensitivity()
    {
        Color[] complete = Draw(false);
        for (int i = 0; i < Layers.Brushes.Count; i++)
        {
            IFillBrush layer = Layers.Brushes[i];
            Layers.Brushes.RemoveAt(i);
            Cached.InvalidateVisual();
            Color[] missing = Draw(false);
            int visibleDifferences = complete.Where((pixel, index) => Math.Abs(pixel.R - missing[index].R) > 8
                || Math.Abs(pixel.G - missing[index].G) > 8 || Math.Abs(pixel.B - missing[index].B) > 8).Count();
            Check(visibleDifferences > 100, "Omitting layer " + i + " did not independently change enough native pixels.");
            Sensitivities.Add(new { layer = i, pixelsWithChannelErrorAbove8 = visibleDifferences });
            Layers.Brushes.Insert(i, layer);
            Cached.InvalidateVisual();
        }
        Pass("layer-omission-controls-exceed-quantization-allowances");
    }

    private void TestTransparentLayers()
    {
        IFillBrush opaqueLighting = Layers.Brushes[0];
        Layers.Brushes.RemoveAt(0);
        Cached.InvalidateVisual();
        foreach (bool transparent in new[] { true, false })
        {
            Color[] normal = Draw(false, transparent: transparent);
            Warm();
            long hits = Desktop.RenderCaches.Statistics.Hits;
            Compare(transparent ? "transparent-layers-over-transparent-scene" : "transparent-layers-over-opaque-scene",
                normal, Draw(true, transparent: transparent), allowedChannelError: transparent ? 0 : 1);
            Check(Desktop.RenderCaches.Statistics.Hits == hits + 4, "Transparent graph did not use cached targets.");
        }
        Layers.Brushes.Insert(0, opaqueLighting);
        Cached.InvalidateVisual();
        CompareWarm("opaque-local-lighting-restored");
        Pass("transparent-and-opaque-scene-layered-parity");
    }

    private void TestEffectNineSlice()
    {
        using Effect sliceEffect = Lighting.Clone();
        var slices = new MGNineSliceFillBrush(sliceEffect, new(7), new MGTextureData(Tiles), new(3),
            new MGCompositedFillBrush(new MGSolidFillBrush(Color.DarkSlateBlue), ThemeColor)) { UseStandardParameters = true };
        Cached.Brush = new MGCompositedFillBrush(new MGSolidFillBrush(Color.DarkBlue), slices);
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            Desktop.SetUniformUIScale(scale);
            Settle();
            sliceEffect.Parameters["CustomInt"].SetValue(101);
            Cached.InvalidateVisual();
            CompareWarm("effect-bearing-nine-slice-local-uv-scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                gradientQuantization: scale == 1.5f || scale == 2f);
            sliceEffect.Parameters["CustomInt"].SetValue(100);
            Cached.InvalidateVisual();
            CompareWarm("effect-bearing-nine-slice-texture-sampling-scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Desktop.SetUniformUIScale(1);
        Settle();
        sliceEffect.Parameters["CustomInt"].SetValue(101);
        Cached.InvalidateVisual();
        Warm();
        Cached.SourceMode = MGRenderCacheSourceMode.Tracked;
        AssertBypass("effect-bearing-nine-slice-tracked-bypass");
        Cached.SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic;
        foreach (bool late in new[] { false, true })
        {
            Warm();
            Color[] prior = Draw(true);
            if (!late)
            {
                Update();
                Draw(true);
                Update();
            }
            long hits = Desktop.RenderCaches.Statistics.Hits;
            long builds = Desktop.RenderCaches.Statistics.Builds;
            sliceEffect.Parameters["CustomInt"].SetValue(late ? 101 : 100);
            Cached.InvalidateVisual();
            if (!late)
            {
                Desktop.PrepareRenderCaches();
                Check(Desktop.RenderCaches.Statistics.Builds == builds, "Stale effect-nine-slice observation captured.");
            }
            Color[] changed = Draw(true);
            Check(Desktop.RenderCaches.Statistics.Hits == hits && prior.Where((c, i) => c != changed[i]).Any(),
                "Effect-nine-slice parameter mutation reused stale or unchanged pixels.");
            string name = "effect-bearing-nine-slice-parameter-" + (late ? "late-after-prepare" : "before-prepare");
            Compare(name, changed, Draw(false));
            CompareWarm(name + "-rebuild");
        }
        Cached.Brush = Layers;
        Pass("effect-bearing-nine-slice-uv-lighting-and-manual-invalidation");
    }

    private void TestInvalidation()
    {
        foreach (string mutation in new[] { "effect-parameter", "texture-setdata", "raw-brush-list" })
        {
            foreach (bool late in new[] { false, true })
            {
                Warm();
                Color[] before = Draw(true);
                if (!late)
                {
                    Update();
                    Draw(true);
                    Update();
                }
                long builds = Desktop.RenderCaches.Statistics.Builds;
                long hits = Desktop.RenderCaches.Statistics.Hits;
                if (mutation == "effect-parameter")
                {
                    Tint.Parameters["CustomColor"].SetValue((new Color(150, late ? 60 : 120, 20) * 0.375f).ToVector4());
                }
                else if (mutation == "texture-setdata")
                {
                    UploadTiles(!late);
                }
                else
                {
                    Layers.Brushes.Add(new MGSolidFillBrush((late ? Color.DarkGreen : Color.Purple) * 0.25f));
                }
                Cached.InvalidateVisual();
                if (!late)
                {
                    Desktop.PrepareRenderCaches();
                    Check(Desktop.RenderCaches.Statistics.Builds == builds, "Invalid pending source captured stale " + mutation);
                }
                Color[] direct = Draw(true);
                Check(Desktop.RenderCaches.Statistics.Hits == hits, "Invalidated source hit stale " + mutation);
                Compare(mutation + (late ? "-late-after-prepare" : "-before-prepare"), direct, Draw(false));
                Check(before.Where((c, i) => c != direct[i]).Any(), "Mutation did not alter native pixels: " + mutation);
                CompareWarm(mutation + (late ? "-late-rebuild" : "-rebuild"));
            }
        }
        Pass("opaque-effect-texture-list-invalidation-before-and-after-prepare");
        Warm();
        ThemeColor.Brush = new MGSolidFillBrush(Desktop.Theme.TreeViewExpanderArrowColor * 0.375f);
        long hitCount = Desktop.RenderCaches.Statistics.Hits;
        Compare("nested-theme-color-observable-invalidation", Draw(true), Draw(false));
        Check(Desktop.RenderCaches.Statistics.Hits == hitCount, "Nested color mutation reused old pixels.");
        CompareWarm("nested-theme-color-rebuild");
        Pass("nested-color-dependency-invalidates");
    }

    private void TestIsolationAndModes()
    {
        Warm();
        Cached.SourceMode = MGRenderCacheSourceMode.Tracked;
        AssertBypass("tracked-effect-graph-bypass");
        Cached.SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic;
        CompareWarm("explicit-static-mode-recovery");
        var copy = (MGCachedFillBrush)Cached.Copy();
        Check(copy.SourceMode == Cached.SourceMode && copy.IsCacheEnabled == Cached.IsCacheEnabled
            && !ReferenceEquals(copy.Brush, Cached.Brush), "Copy lost policy or source independence.");
        Wrappers.Add(copy);
        Owners[1].BackgroundBrush = new(copy);
        Owners[1].OverlayBrush = copy;
        CompareWarm("copied-wrapper-owner-site-isolation");
        Check(Desktop.RenderCaches.EntryCount == 4, "Copied wrapper merged independent targets.");
        copy.SourceMode = MGRenderCacheSourceMode.Tracked;
        long hits = Desktop.RenderCaches.Statistics.Hits;
        Draw(true);
        Check(Desktop.RenderCaches.Statistics.Hits == hits + 2, "One wrapper mode changed sibling eligibility.");
        copy.SourceMode = MGRenderCacheSourceMode.ExplicitlyStatic;
        Warm();
        Owners[0].IsRenderVolatile = true;
        Owners[1].IsRenderVolatile = true;
        AssertBypass("owner-volatility-overrides-static-assertion");
        foreach (MGRectangle owner in Owners) { owner.IsRenderVolatile = false; }
        var volatileSource = new VolatileSource();
        Cached.Brush = new MGCompositedFillBrush(Layers, volatileSource);
        copy.Brush = new MGCompositedFillBrush(copy.Brush, volatileSource);
        AssertBypass("known-source-volatility-overrides-static-assertion");
        Cached.Brush = Layers;
        copy.Brush = Layers.Copy();
        CompareWarm("volatile-removal-recovers");
        Pass("source-mode-copy-and-known-volatility-isolation");
    }

    private void TestFallbacks()
    {
        using BasicEffect outerEffect = new(GraphicsDevice)
        {
            TextureEnabled = true, VertexColorEnabled = true,
            World = Matrix.Identity, View = Matrix.Identity,
            Projection = Matrix.CreateOrthographicOffCenter(0, 640, 480, 0, 0, 1)
        };
        foreach (string name in new[] { "fractional-opacity", "outer-effect", "additive-blend", "linear-sampler", "scaled-transform", "fractional-translation" })
        {
            Warm();
            long hits = Desktop.RenderCaches.Statistics.Hits;
            float opacity = name == "fractional-opacity" ? 0.5f : 1;
            DrawSettings settings = name switch
            {
                "outer-effect" => DrawSettings.Default with { Effect = outerEffect },
                "additive-blend" => DrawSettings.Default with { BlendType = BlendType.Additive },
                "linear-sampler" => DrawSettings.Default with { SamplerType = SamplerType.LinearClamp },
                "scaled-transform" => new(Matrix.CreateScale(1.125f)),
                "fractional-translation" => new(Matrix.CreateTranslation(0.5f, 0.25f, 0)),
                _ => DrawSettings.Default
            };
            Color[] fallback = Draw(true, opacity: opacity, settings: settings);
            Check(Desktop.RenderCaches.Statistics.Hits == hits && Desktop.RenderCaches.PendingCount == 0,
                "Unsupported cached-context draw reused or queued before reference toggle: " + name);
            Compare(name + "-fallback", fallback, Draw(false, opacity: opacity, settings: settings));
            Check(Desktop.RenderCaches.Statistics.Hits == hits && Desktop.RenderCaches.PendingCount == 0,
                "Unsupported final context hit or queued: " + name);
        }
        Pass("fractional-opacity-effect-blend-sampler-transform-fallback");
        Pass("unsupported-context-never-queues-before-reference-toggle");
        Warm();
        Color[] movingA = Draw(true, world: 7);
        Color[] movingB = Draw(true, world: 29);
        Compare("moving-scene-cached-background", movingB, Draw(false, world: 29));
        Check(movingA[450 * 640 + 620] != movingB[450 * 640 + 620], "Moving sentinel did not move outside UI.");
        Pass("moving-world-sentinel-preserved");
    }

    private void TestWarmAndLifecycle()
    {
        Warm();
        long builds = Desktop.RenderCaches.Statistics.Builds;
        long allocations = Desktop.RenderCaches.Statistics.Allocations;
        long hits = Desktop.RenderCaches.Statistics.Hits;
        for (int i = 0; i < 24; i++)
        {
            Update();
            Desktop.PrepareRenderCaches();
            Draw(true, world: i);
        }
        Check(Desktop.RenderCaches.Statistics.Builds == builds && Desktop.RenderCaches.Statistics.Allocations == allocations
            && Desktop.RenderCaches.Statistics.Hits == hits + 96, "Warm idle rebuilt, allocated, or missed a site.");
        WarmIdleEvidence = new { frames = 24, buildsBefore = builds, buildsAfter = Desktop.RenderCaches.Statistics.Builds,
            allocationsBefore = allocations, allocationsAfter = Desktop.RenderCaches.Statistics.Allocations,
            hitsBefore = hits, hitsAfter = Desktop.RenderCaches.Statistics.Hits };
        Pass("warm-idle-build-and-allocation-stop");
        RenderTarget2D[] ready = LiveTargets();
        using (DrawTransaction transaction = new(Desktop.Renderer, DrawSettings.Default, true))
        {
            Desktop.Draw(transaction);
            Cached.InvalidateVisual();
            Check(ready.All(x => !x.IsDisposed), "Invalidation disposed queued sampled targets during a transaction.");
        }
        Update();
        Desktop.PrepareRenderCaches();
        Check(ready.Where(x => !LiveTargets().Contains(x)).All(x => x.IsDisposed), "Retired targets survived safe preparation.");
        Pass("sampled-target-retirement-waits-for-safe-point");
        Warm();
        ready = LiveTargets();
        Desktop.RenderCaches.MaximumEntries = 1;
        Check(Desktop.RenderCaches.EntryCount == 1 && ready.Count(x => x.IsDisposed) == 3, "Entry budget did not evict immediately.");
        Compare("one-entry-budget-fallback", Draw(true), Draw(false));
        Desktop.RenderCaches.MaximumEntries = 64;
        Desktop.RenderCaches.MaximumRetainedBytes = ready.Min(x => (long)x.Width * x.Height * 4);
        Check(Desktop.RenderCaches.RetainedBytes <= Desktop.RenderCaches.MaximumRetainedBytes && Desktop.RenderCaches.EntryCount <= 1,
            "Positive byte budget retained excess native bytes.");
        Desktop.RenderCaches.MaximumRetainedBytes = 0;
        Check(Desktop.RenderCaches.EntryCount == 0 && Desktop.RenderCaches.RetainedBytes == 0 && ready.All(x => x.IsDisposed),
            "Zero byte budget retained targets.");
        AssertBypass("zero-budget-direct-parity");
        Desktop.RenderCaches.MaximumRetainedBytes = 33_554_432;
        Desktop.RenderCaches.MaximumEntries = 64;
        Desktop.RenderCaches.MaximumTargetDimension = 8;
        AssertBypass("dimension-rejection-direct-parity");
        Desktop.RenderCaches.MaximumTargetDimension = 4096;
        CompareWarm("budget-and-dimension-recovery");
        Pass("entry-byte-dimension-budgets-evict-and-recover");
        TestNativeFailure();
        Warm();
        ready = LiveTargets();
        int resetting = 0, reset = 0;
        EventHandler<EventArgs> onResetting = (_, _) => resetting++;
        EventHandler<EventArgs> onReset = (_, _) => reset++;
        GraphicsDevice.DeviceResetting += onResetting;
        GraphicsDevice.DeviceReset += onReset;
        GraphicsDevice.Reset();
        GraphicsDevice.DeviceResetting -= onResetting;
        GraphicsDevice.DeviceReset -= onReset;
        Check(resetting > 0 && reset > 0 && ready.All(x => x.IsDisposed) && Desktop.RenderCaches.EntryCount == 0,
            "Real native reset did not release cache entries.");
        ResetExercised = true;
        CompareWarm("device-reset-rebuild-native-parity");
        Pass("native-reset-releases-and-rebuilds-brush-targets");
        ready = LiveTargets();
        MGRenderCacheManager manager = Desktop.RenderCaches;
        Desktop.DisposeRenderCaches();
        Check(manager.IsDisposed && ReferenceEquals(manager, Desktop.RenderCaches) && manager.EntryCount == 0
            && manager.RetainedBytes == 0 && ready.All(x => x.IsDisposed), "Desktop disposal left targets or resurrected service.");
        Update();
        Desktop.PrepareRenderCaches();
        Compare("disposed-desktop-direct-parity", Draw(true), Draw(false));
        Pass("desktop-teardown-disposes-all-owned-targets");
    }

    private void TestNativeFailure()
    {
        Desktop.RenderCaches.Clear();
        FieldInfo allocatorField = typeof(MGRenderCacheManager).GetField("Allocator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = (IMGRenderCacheAllocator)allocatorField.GetValue(Desktop.RenderCaches)!;
        var failing = new NativeFailureAllocator(GraphicsDevice);
        allocatorField.SetValue(Desktop.RenderCaches, failing);
        try
        {
            Warm(expectEntries: 0);
            Check(failing.Attempts > 0 && failing.Targets.All(x => x.IsDisposed) && Desktop.RenderCaches.LastNativeFailure != null
                && Desktop.RenderCaches.RetainedBytes == 0, "Native allocation failure retained resources or lost diagnostic.");
            FailureTargetsCreated += failing.Targets.Count;
            long failures = failing.Attempts;
            Compare("native-allocation-failure-direct-parity", Draw(true), Draw(false));
            Warm(expectEntries: 0);
            Check(failing.Attempts == failures, "Failed sizes retried every frame.");
        }
        finally
        {
            allocatorField.SetValue(Desktop.RenderCaches, original);
            Desktop.RenderCaches.MaximumTargetDimension++;
        }
        CompareWarm("native-failure-policy-recovery");
        Pass("native-target-failure-disposal-suppression-and-direct-parity");
    }

    private void Settle()
    {
        for (int i = 0; i < 4; i++) { Update(); Draw(false); }
    }
    private void SettleEnabled()
    {
        for (int i = 0; i < 4; i++)
        {
            Update();
            Desktop.PrepareRenderCaches();
            Draw(true);
            TrackTargets();
        }
        Check(Desktop.RenderCaches.EntryCount == 4, "Enabled geometry settlement did not rebuild four sites.");
    }
    private void Update() => Fixture.Update(0);
    private void Warm(int expectEntries = 4)
    {
        Update();
        Draw(true);
        Update();
        Desktop.PrepareRenderCaches();
        TrackTargets();
        Check(Desktop.RenderCaches.EntryCount == expectEntries, "Warm entry count: " + Desktop.RenderCaches.EntryCount + " expected " + expectEntries);
    }
    private void CompareWarm(string name, bool gradientQuantization = false)
    {
        Color[] normal = Draw(false);
        long hits = Desktop.RenderCaches.Statistics.Hits;
        Color[] cold = Draw(true);
        Compare(name + "-first-miss", normal, cold);
        Check(Desktop.RenderCaches.Statistics.Hits == hits, "First miss borrowed an invalid target: " + name);
        Warm();
        hits = Desktop.RenderCaches.Statistics.Hits;
        Compare(name + "-warm", normal, Draw(true), gradientQuantization ? 1 : 0, gradientOnly: gradientQuantization);
        Check(Desktop.RenderCaches.Statistics.Hits == hits + 4, "A/B warm comparison did not hit all four sites: " + name);
    }
    private void AssertBypass(string name)
    {
        long hits = Desktop.RenderCaches.Statistics.Hits;
        Color[] fallback = Draw(true);
        Check(Desktop.RenderCaches.Statistics.Hits == hits && Desktop.RenderCaches.PendingCount == 0,
            "Bypass cached draw reused or queued before reference toggle: " + name);
        Compare(name, fallback, Draw(false));
        Update();
        Desktop.PrepareRenderCaches();
        Check(Desktop.RenderCaches.Statistics.Hits == hits && Desktop.RenderCaches.PendingCount == 0, "Bypass reused or queued: " + name);
        Pass(name);
    }
    private Color[] Draw(bool enabled, int world = 7, float opacity = 1, DrawSettings? settings = null, Rectangle? clip = null, bool transparent = false)
    {
        foreach (MGCachedFillBrush wrapper in Wrappers) { wrapper.IsCacheEnabled = enabled; }
        GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
        GraphicsDevice.Clear(transparent ? Color.Transparent : new Color(13, 29, 41));
        using (DrawTransaction transaction = new(Desktop.Renderer, DrawSettings.Default, true))
        {
            transaction.FillRectangle(Vector2.Zero, new Rectangle(600, 440, 40, 40), world % 2 == 0 ? Color.Gold : new Color(world, 80, 140));
        }
        GraphicsDevice.ScissorRectangle = clip ?? GraphicsDevice.Viewport.Bounds;
        long switches = GraphicsDevice.Metrics.TargetCount;
        long allocations = Desktop.RenderCaches.Statistics.Allocations;
        Desktop.Draw(opacity, settings);
        Check(GraphicsDevice.Metrics.TargetCount == switches && Desktop.RenderCaches.Statistics.Allocations == allocations,
            "Final background composition switched targets or allocated.");
        Color[] pixels = new Color[640 * 480];
        GraphicsDevice.GetBackBufferData(pixels);
        return pixels;
    }
    private RenderTarget2D[] LiveTargets(MGElement? owner = null)
    {
        IEnumerable entries = (IEnumerable)typeof(MGRenderCacheManager).GetField("Entries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Desktop.RenderCaches)!;
        return entries.Cast<MGRenderCacheManager.Entry>()
            .Where(x => owner == null || (x.Request.Registration.TryGetIdentities(out var registeredOwner, out _) && ReferenceEquals(owner, registeredOwner)))
            .Select(x => x.Resource.Texture).ToArray();
    }
    private void TrackTargets()
    {
        foreach (RenderTarget2D target in LiveTargets())
        {
            if (!Targets.Contains(target)) { Targets.Add(target); }
        }
    }
    private void UploadTiles(bool alternate)
    {
        Color[] pixels = new Color[144];
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                bool edge = x < 3 || x >= 9 || y < 3 || y >= 9;
                pixels[y * 12 + x] = (alternate ? (edge ? Color.Coral : Color.DarkCyan) : (edge ? Color.Teal : Color.DarkBlue)) * 0.5f;
            }
        }
        Tiles.SetData(pixels);
    }
    private void Compare(string name, Color[] expected, Color[] actual, int allowedChannelError = 0, bool gradientOnly = false)
    {
        int[] channelMaxima = new int[4];
        int[] channelCounts = new int[4];
        int[] channelAllowances = [allowedChannelError, allowedChannelError, gradientOnly ? 0 : allowedChannelError, 0];
        int different = 0, maximum = 0, first = -1;
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                different++;
                if (first < 0) { first = i; }
                int[] errors = [Math.Abs(expected[i].R - actual[i].R), Math.Abs(expected[i].G - actual[i].G),
                    Math.Abs(expected[i].B - actual[i].B), Math.Abs(expected[i].A - actual[i].A)];
                for (int channel = 0; channel < 4; channel++)
                {
                    channelMaxima[channel] = Math.Max(channelMaxima[channel], errors[channel]);
                    if (errors[channel] > 0) { channelCounts[channel]++; }
                }
                maximum = Math.Max(maximum, errors.Max());
            }
        }
        Directory.CreateDirectory(EvidenceDirectory);
        string expectedPath = SavePixels(name + "-normal", expected);
        string actualPath = SavePixels(name + "-cached", actual);
        string? differencePath = null;
        if (different > 0)
        {
            differencePath = SavePixels(name + "-difference-x64", expected.Select((pixel, index) => new Color(
                Math.Min(255, Math.Abs(pixel.R - actual[index].R) * 64), Math.Min(255, Math.Abs(pixel.G - actual[index].G) * 64),
                Math.Min(255, Math.Abs(pixel.B - actual[index].B) * 64), 255)).ToArray());
        }
        Diffs.Add(new(name, different, maximum, first < 0 ? null : new PixelLocation(first % 640, first / 640), allowedChannelError,
            channelMaxima, channelCounts, channelAllowances, expectedPath, actualPath, differencePath));
        Check(channelMaxima.Select((error, channel) => error <= channelAllowances[channel]).All(x => x),
            name + " exceeds per-channel allowance: " + string.Join(",", channelMaxima));
        Check(maximum <= allowedChannelError, name + " differs at " + first + ": " + different + " pixels, max channel error " + maximum);
    }
    private string SavePixels(string name, Color[] pixels)
    {
        string path = Path.Combine(EvidenceDirectory, name + ".png");
        using Texture2D texture = new(GraphicsDevice, 640, 480);
        texture.SetData(pixels);
        using FileStream stream = File.Create(path);
        texture.SaveAsPng(stream, 640, 480);
        return path;
    }
    private static string AssemblyHash(Assembly assembly)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
    private void Pass(string name) => Results.Add(name, true);
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
    protected override void Update(GameTime gameTime) { Exit(); }
    protected override void Draw(GameTime gameTime) { }
    private sealed record PixelDiff(string Name, int DifferentPixels, int MaximumChannelError, PixelLocation? FirstDifference, int AllowedChannelError, int[] ChannelMaximaRGBA, int[] DifferentPixelsPerChannelRGBA, int[] AllowedErrorsRGBA, string NormalImage, string CachedImage, string? DifferenceImage);
    private sealed record PixelLocation(int X, int Y);
    private sealed class VolatileSource : IFillBrush, IMGRenderCacheSource
    {
        public ulong VisualRevision => 1;
        public bool IsRenderVolatile => true;
        public bool SupportsRenderCaching => true;
        public event EventHandler? VisualInvalidated { add { } remove { } }
        public IFillBrush Copy() => new VolatileSource();
        public void Draw(ElementDrawArgs args, MGElement owner, Rectangle bounds) { }
    }
    private sealed class NativeFailureAllocator(GraphicsDevice device) : IMGRenderCacheAllocator
    {
        internal int Attempts;
        internal readonly List<RenderTarget2D> Targets = new();
        public IMGRenderCacheTarget Create(int width, int height)
        {
            Attempts++;
            using RenderTarget2D target = new(device, width, height);
            Targets.Add(target);
            device.SetRenderTarget(target);
            device.Clear(Color.CornflowerBlue);
            device.SetRenderTarget(null);
            Color[] pixels = new Color[width * height];
            target.GetData(pixels);
            Check(pixels.All(x => x == Color.CornflowerBlue), "Failure injection did not first exercise genuine native graphics.");
            throw new OutOfMemoryException("T14 controlled failure after real target construction, first bind and readback.");
        }
    }
}
