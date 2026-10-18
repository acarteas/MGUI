using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
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
            if (args.Length == 1 && args[0] == "--sample")
            {
                return SampleTests.RunAllNative();
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
                mode = "normal",
                options.Scenario,
                options.Width,
                options.Height,
                uiScale = options.Scale,
                options.Seed,
                warmupFrames = options.Warmup,
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

internal sealed record Options(string Scenario, int Width, int Height, float Scale, int Seed, int Warmup, int Frames, string Output, string Screenshot)
{
    public static readonly string[] Scenarios = ["batched", "layered", "hud", "animated-score", "scrolling", "nested-clips"];

    public static Options Parse(string[] args)
    {
        if (args.Length != 9)
        {
            throw new ArgumentException("Usage: <scenario> <width> <height> <scale> <seed> <warmup> <frames> <output.json> <screenshot.png>");
        }

        Options result = new(args[0], int.Parse(args[1]), int.Parse(args[2]), float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]), args[7], args[8]);
        if (!Scenarios.Contains(result.Scenario) || result.Width < 320 || result.Height < 240 || !float.IsFinite(result.Scale) || result.Scale <= 0 || result.Warmup < 0 || result.Frames < 1)
        {
            throw new ArgumentException("Invalid benchmark configuration.");
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

internal readonly record struct FrameSample(int Frame, WorkloadState State, Phase UpdateLayout, Phase WorldSubmission, Phase UiSubmission, Phase Present, Counters World, Counters Ui, Counters Total);

internal sealed class BenchmarkGame : Game
{
    private readonly Options Options;
    private readonly GraphicsDeviceManager Graphics;
    private BenchmarkFixture Fixture = null!;
    private SpriteBatch WorldBatch = null!;
    private Texture2D WhitePixel = null!;
    private Phase PendingUpdate;
    private int UpdatesForFrame;
    private FrameSample PendingSample;
    public FrameSample[] Samples { get; }
    public int PresentedFrames { get; private set; }
    public string? ScreenshotHash { get; private set; }
    public object? PixelValidation { get; private set; }

    public BenchmarkGame(Options options)
    {
        Options = options;
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
        Fixture = new(this, Options);
    }

    protected override void Update(GameTime gameTime)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        Fixture.Update(PresentedFrames);
        Phase phase = Phase.Since(ticks, bytes);
        PendingUpdate = new(PendingUpdate.CpuMs + phase.CpuMs, PendingUpdate.AllocatedBytes + phase.AllocatedBytes);
        UpdatesForFrame++;
    }

    protected override void Draw(GameTime gameTime)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
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
        if (UpdatesForFrame == 0 || afterUi.DrawCount == before.DrawCount || afterUi.PrimitiveCount == before.PrimitiveCount)
        {
            throw new InvalidOperationException("Frame did not update and submit native graphics work.");
        }

        PendingSample = new(PresentedFrames, Fixture.State, PendingUpdate, world, ui, default,
            Counters.From(afterWorld - before), Counters.From(afterUi - afterWorld), Counters.From(afterUi - before));
        PendingUpdate = default;
        UpdatesForFrame = 0;

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
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        base.EndDraw();
        Phase present = Phase.Since(ticks, bytes);
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
        Fixture.Dispose();
        WhitePixel.Dispose();
        WorldBatch.Dispose();
        base.UnloadContent();
    }
}
