using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--stability")
            {
                return StabilityGame.Run(args[1]);
            }
            if (args.Length == 1 && args[0] == "--resources")
            {
                return ResourceTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--preparation")
            {
                return PreparationTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--cached-brush")
            {
                return CachedBrushTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--collection-controls")
            {
                return CollectionControlTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--interactive")
            {
                return InteractiveTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--background-parity")
            {
                return BackgroundParityTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--cached-brush-xaml")
            {
                return CachedBrushXamlTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--text-images")
            {
                return TextImageTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--eligibility")
            {
                return EligibilityTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--decorations")
            {
                return DecorationTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--content-boundary")
            {
                return ContentBoundaryTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--render-cache-xaml")
            {
                return RenderCacheXamlTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--composition")
            {
                return CompositionTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--lifetime")
            {
                return LifetimeTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--live-siblings")
            {
                return LiveSiblingTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--normal-guards")
            {
                return NormalPathTests.RunAllNative();
            }
            if (args.Length == 1 && args[0] == "--sample")
            {
                return SampleTests.RunAllNative();
            }
            if (args.Length == 4 && args[0] == "--matrix")
            {
                return LiveSiblingTests.RunMatrix(int.Parse(args[1]), int.Parse(args[2]),
                    float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            }
            Options options = Options.Parse(args);
            using BenchmarkGame game = new(options);
            game.Run();
            if (game.PresentedFrames != options.Warmup + options.Frames + 1)
            {
                throw new InvalidOperationException("Benchmark ended before all requested frames were presented.");
            }

            File.WriteAllText(options.Output, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                mode = options.Mode,
                options.Scenario,
                options.Width,
                options.Height,
                uiScale = options.Scale,
                options.Seed,
                warmupFrames = options.Warmup,
                drawableWarmupPresents = options.IsComparison ? 3 : 0,
                measuredPresentedFrames = options.Frames,
                totalPresentedFrames = game.PresentedFrames,
                backend = "MonoGame DesktopGL",
                framework = typeof(Game).Assembly.FullName,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                graphicsProfile = game.GraphicsDevice.GraphicsProfile.ToString(),
                presentationInterval = game.GraphicsDevice.PresentationParameters.PresentationInterval.ToString(),
                textEngine = "FontStashSharp 1.5.4 / NotoSans-Regular",
                gpuTimeMs = (double?)null,
                gpuTimeUnavailableReason = "MonoGame DesktopGL exposes no portable GPU timer query API. Submission/present CPU times are not GPU execution times.",
                screenshot = options.Screenshot,
                screenshotSha256 = game.ScreenshotHash,
                nativePixelValidation = game.PixelValidation,
                coldFrames = game.ColdSamples,
                stateTrace = game.StateTrace,
                parityCaptureEnabled = options.CaptureParity,
                parityFrames = game.ParityFrames,
                frames = game.Samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

internal sealed record Options(string Scenario, int Width, int Height, float Scale, int Seed, int Warmup, int Frames, string Output, string Screenshot, string Mode = "normal")
{
    public static readonly string[] Scenarios = ["batched", "layered", "hud", "animated-score", "scrolling", "nested-clips"];
    public static readonly string[] ComparisonScenarios = ["compare-cells", "compare-layered", "compare-mixed", "compare-dirty", "compare-scroll", "compare-change", "compare-churn"];
    public bool CaptureParity { get; } = Environment.GetEnvironmentVariable("MGUI_COMPARISON_PARITY") == "1";
    public bool IsComparison => Scenario.StartsWith("compare-", StringComparison.Ordinal);

    public static Options Parse(string[] args)
    {
        if (args.Length is not (9 or 10))
        {
            throw new ArgumentException("Usage: <scenario> <width> <height> <scale> <seed> <warmup> <frames> <output.json> <screenshot.png> [normal|disabled|normal-batching|background|panel]");
        }

        Options result = new(args[0], int.Parse(args[1]), int.Parse(args[2]), float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]), args[7], args[8], args.Length == 10 ? args[9] : "normal");
        if (result.Mode is not ("normal" or "disabled" or "normal-batching" or "background" or "panel") || !(Scenarios.Contains(result.Scenario) || ComparisonScenarios.Contains(result.Scenario)) || result.Width < 320 || result.Height < 240 || !float.IsFinite(result.Scale) || result.Scale <= 0 || result.Warmup < 0 || result.Frames < 1)
        {
            throw new ArgumentException("Invalid benchmark configuration.");
        }

        if (!result.IsComparison && result.Mode is not ("normal" or "disabled" or "normal-batching"))
        {
            throw new ArgumentException("Cache comparison modes require an explicitly named compare-* fixture.");
        }

        return result;
    }
}

internal readonly record struct Phase(double CpuMs, long AllocatedBytes)
{
    public static Phase Since(long ticks, long bytes) => new(Stopwatch.GetElapsedTime(ticks).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
}

internal readonly record struct Counters(long DrawCalls, long Primitives, long Sprites, long TargetSwitches)
{
    public static Counters From(GraphicsMetrics metrics) => new(metrics.DrawCount, metrics.PrimitiveCount, metrics.SpriteCount, metrics.TargetCount);
}

internal readonly record struct CacheCounters(long Allocations, long Builds, long TargetSwitches, long RetainedBytes, int Entries, int Pending, long Hits = 0, long Misses = 0, long Evictions = 0, long Disposals = 0);

internal readonly record struct FrameSample(int Frame, WorkloadState State, Phase UpdateLayout, Phase Prepare, Phase WorldSubmission, Phase UiSubmission, Phase Present, Counters World, Counters Ui, Counters Total, CacheCounters Cache, long TextureChanges = 0, ComparisonState? Comparison = null);

internal sealed class BenchmarkGame : Game
{
    private readonly Options Options;
    private readonly GraphicsDeviceManager Graphics;
    private BenchmarkFixture Fixture = null!;
    private SpriteBatch WorldBatch = null!;
    private Texture2D WhitePixel = null!;
    private Phase PendingUpdate;
    private int UpdatesForFrame;
    private int DrawableWarmupsRemaining;
    private FrameSample PendingSample;
    public FrameSample[] Samples { get; }
    public List<FrameSample> ColdSamples { get; } = new();
    public object? StateTrace { get; private set; }
    public List<object> ParityFrames { get; } = new();
    public int PresentedFrames { get; private set; }
    public string? ScreenshotHash { get; private set; }
    public object? PixelValidation { get; private set; }

    public BenchmarkGame(Options options)
    {
        Options = options;
        DrawableWarmupsRemaining = options.IsComparison ? 3 : 0;
        Samples = new FrameSample[options.Frames];
        Graphics = new(this)
        {
            PreferredBackBufferWidth = options.Width,
            PreferredBackBufferHeight = options.Height,
            SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        Window.Title = $"MGUI render baseline: {options.Scenario}";
    }

    protected override void LoadContent()
    {
        WorldBatch = new(GraphicsDevice);
        WhitePixel = new(GraphicsDevice, 1, 1);
        WhitePixel.SetData(new[] { Color.White });
        if (DrawableWarmupsRemaining == 0)
        {
            Fixture = new(this, Options);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (DrawableWarmupsRemaining > 0)
        {
            return;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        Fixture.Update(PresentedFrames);
        Phase phase = Phase.Since(ticks, bytes);
        PendingUpdate = new(PendingUpdate.CpuMs + phase.CpuMs, PendingUpdate.AllocatedBytes + phase.AllocatedBytes);
        UpdatesForFrame++;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (DrawableWarmupsRemaining > 0)
        {
            GraphicsDevice.Clear(Color.Black);
            return;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        DrawTransactionTrace.Start(PresentedFrames == Options.Warmup);
        Fixture.Desktop.PrepareRenderCaches();
        Phase prepare = Phase.Since(ticks, bytes);
        bytes = GC.GetAllocatedBytesForCurrentThread();
        ticks = Stopwatch.GetTimestamp();
        GraphicsMetrics before = GraphicsDevice.Metrics;
        GraphicsDevice.Clear(new Color(16, 22, 35));
        WorldBatch.Begin(blendState: BlendState.Opaque);
        int offset = PresentedFrames % 64;
        for (int y = -64; y < Options.Height; y += 64)
        {
            for (int x = -64; x < Options.Width; x += 64)
            {
                Color color = ((x / 64 + y / 64) & 1) == 0 ? new(42, 67, 91) : new(75, 97, 118);
                WorldBatch.Draw(WhitePixel, new Rectangle(x + offset, y, 64, 64), color);
            }
        }

        WorldBatch.End();
        Phase world = Phase.Since(ticks, bytes);
        GraphicsMetrics afterWorld = GraphicsDevice.Metrics;
        bytes = GC.GetAllocatedBytesForCurrentThread();
        ticks = Stopwatch.GetTimestamp();
        Fixture.Desktop.Draw();
        Phase ui = Phase.Since(ticks, bytes);
        GraphicsMetrics afterUi = GraphicsDevice.Metrics;
        object? trace = DrawTransactionTrace.Finish();
        if (trace != null)
        {
            StateTrace = trace;
        }
        if (UpdatesForFrame == 0 || afterUi.DrawCount == before.DrawCount || afterUi.PrimitiveCount == before.PrimitiveCount)
        {
            throw new InvalidOperationException("Frame did not update and submit native graphics work.");
        }

        PendingSample = new(PresentedFrames, Fixture.State, PendingUpdate, prepare, world, ui, default,
            Counters.From(afterWorld - before), Counters.From(afterUi - afterWorld), Counters.From(afterUi - before), Fixture.GetCacheCounters(), afterUi.TextureCount - afterWorld.TextureCount, Fixture.GetComparisonState());
        PendingUpdate = default;
        UpdatesForFrame = 0;

        // Untimed correctness-only readbacks. The performance runner rejects this marked mode.
        int changeFrame = Options.Warmup + Options.Frames / 2;
        if (Options.CaptureParity && Options.IsComparison && (PresentedFrames <= 3
            || PresentedFrames == Options.Warmup - 1 || Math.Abs(PresentedFrames - changeFrame) <= 4))
        {
            Color[] parityPixels = new Color[Options.Width * Options.Height];
            GraphicsDevice.GetBackBufferData(parityPixels);
            string path = Options.Screenshot + ".frame-" + PresentedFrames + ".png";
            using Texture2D image = new(GraphicsDevice, Options.Width, Options.Height);
            image.SetData(parityPixels);
            using (FileStream stream = File.Create(path))
            {
                image.SaveAsPng(stream, Options.Width, Options.Height);
            }
            ParityFrames.Add(new { frame = PresentedFrames, state = Fixture.State, comparison = Fixture.GetComparisonState(),
                cache = Fixture.GetCacheCounters(), screenshot = path,
                screenshotSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() });
        }

        // Capture on one extra unmeasured frame, so readback/PNG encoding never enters samples.
        if (PresentedFrames == Options.Warmup + Options.Frames)
        {
            Color[] pixels = new Color[Options.Width * Options.Height];
            GraphicsDevice.GetBackBufferData(pixels);
            int worldY = Options.Height - 32;
            int worldX = Options.Width - 32;
            bool worldMatches = pixels[worldY * Options.Width + worldX] == ExpectedWorldColor(worldX, worldY, offset)
                && pixels[worldY * Options.Width + worldX - 64] == ExpectedWorldColor(worldX - 64, worldY, offset);
            int changedUiPixels = 0;
            int brightTextPixels = 0;
            for (int y = 48; y < Math.Min(Options.Height - 96, 300); y++)
            {
                for (int x = 48; x < Math.Min(Options.Width - 48, 1000); x++)
                {
                    Color pixel = pixels[y * Options.Width + x];
                    if (pixel != ExpectedWorldColor(x, y, offset))
                    {
                        changedUiPixels++;
                    }

                    if (pixel.R > 180 && pixel.G > 180 && pixel.B > 180)
                    {
                        brightTextPixels++;
                    }
                }
            }

            if (!worldMatches || changedUiPixels < 100 || brightTextPixels < 20)
            {
                throw new InvalidOperationException("Native backbuffer lacks the expected world or visible UI/text.");
            }

            PixelValidation = new { worldSentinelsMatch = worldMatches, changedUiPixels, brightTextPixels, captureFrame = PresentedFrames };
            using Texture2D capture = new(GraphicsDevice, Options.Width, Options.Height);
            capture.SetData(pixels);
            using (FileStream stream = File.Create(Options.Screenshot))
            {
                capture.SaveAsPng(stream, Options.Width, Options.Height);
            }

            ScreenshotHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Options.Screenshot))).ToLowerInvariant();
        }
    }

    private static Color ExpectedWorldColor(int x, int y, int offset)
        => ((((int)Math.Floor((x - offset) / 64.0) + y / 64) & 1) == 0) ? new Color(42, 67, 91) : new Color(75, 97, 118);

    protected override void EndDraw()
    {
        if (DrawableWarmupsRemaining > 0)
        {
            base.EndDraw();
            DrawableWarmupsRemaining--;
            if (DrawableWarmupsRemaining == 0)
            {
                if (Window.ClientBounds.Width != Options.Width || Window.ClientBounds.Height != Options.Height)
                {
                    throw new InvalidOperationException("Drawable did not reach the requested comparison dimensions.");
                }
                Fixture = new(this, Options);
            }
            return;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        base.EndDraw();
        Phase present = Phase.Since(ticks, bytes);
        if (PresentedFrames < 8)
        {
            ColdSamples.Add(PendingSample with { Present = present });
        }
        int index = PresentedFrames - Options.Warmup;
        if (index >= 0 && index < Samples.Length)
        {
            Samples[index] = PendingSample with { Present = present };
        }

        PresentedFrames++;
        if (PresentedFrames == Options.Warmup + Options.Frames + 1)
        {
            Exit();
        }
    }

    protected override void UnloadContent()
    {
        Fixture?.Dispose();
        WhitePixel.Dispose();
        WorldBatch.Dispose();
        base.UnloadContent();
    }
}
