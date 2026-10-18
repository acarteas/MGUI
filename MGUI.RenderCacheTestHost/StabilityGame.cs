using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed record StabilityOptions(string Scenario, string Mode, int Width, int Height, float Scale,
    int Warmup, int Frames, bool VerifyPixels, string Output, string Screenshot)
{
    internal static readonly string[] Scenarios = ["idle-hud", "hover-press", "pulse", "text", "scrolling",
        "resize-scale", "continuous-animation", "resource-soak"];
    internal void Validate()
    {
        if (!Scenarios.Contains(Scenario) || Mode is not ("normal" or "background" or "panel")
            || Width < 1280 || Height < 720 || !float.IsFinite(Scale) || Scale <= 0
            || Warmup < 8 || Frames < 1 || string.IsNullOrWhiteSpace(Output) || string.IsNullOrWhiteSpace(Screenshot))
        {
            throw new ArgumentException("Invalid stability configuration.");
        }
        if (Scenario == "resource-soak" && !VerifyPixels && Frames < 10_000)
        {
            throw new ArgumentException("The resource soak requires at least10000 measured presented frames.");
        }
    }
}

internal readonly record struct StabilityResources(MGRenderCacheStatistics Statistics, long RetainedBytes,
    int Entries, int Pending, long DiscoveryVisits, long SourceReads, long ByteLimit, int EntryLimit);

internal readonly record struct StabilitySample(int Frame, StabilityState State, Phase UpdateLayout, Phase Prepare,
    Phase WorldSubmission, Phase UiSubmission, Phase Present, Counters World, Counters Ui, Counters Total,
    Counters PrepareGraphics, long TextureChanges, long PrepareTextureChanges, StabilityResources Cache);

// Readbacks, image encoding and output serialization happen outside all measured phase scopes.
internal sealed class StabilityGame : Game
{
    private const int DrawableWarmupFrames = 3;
    private readonly StabilityOptions Options;
    private StabilityFixture Fixture = null!;
    private SpriteBatch World = null!;
    private Texture2D Pixel = null!;
    private readonly StabilitySample[] Samples;
    private readonly string?[] PixelHashes;
    private readonly Color[] Pixels;
    private readonly List<object> SentinelCheckpoints = new();
    private Phase PendingUpdate;
    private StabilitySample PendingSample;
    private int Updates;
    private int Presented;
    private long WarmBuilds;
    private MGRenderCacheStatistics WarmStatistics;
    private long WarmBytes;
    private int WarmEntries;
    private long LatePixelChecks;
    private bool DimensionsVerified;
    private string? ScreenshotHash;
    private object? Teardown;
    private int SimulationFrame => Presented - DrawableWarmupFrames;

    private StabilityGame(StabilityOptions options)
    {
        Options = options;
        Samples = new StabilitySample[options.Frames];
        PixelHashes = new string?[options.Frames];
        Pixels = new Color[checked(options.Width * options.Height)];
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = options.Width, PreferredBackBufferHeight = options.Height,
            SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        Window.Title = "MGUI stability: " + options.Scenario + " / " + options.Mode;
    }

    internal static int Run(string configuration)
    {
        StabilityOptions options = JsonSerializer.Deserialize<StabilityOptions>(File.ReadAllText(configuration))
            ?? throw new ArgumentException("Missing stability configuration.");
        options.Validate();
        using StabilityGame game = new(options);
        game.Run();
        if (game.Presented != DrawableWarmupFrames + options.Warmup + options.Frames + 1
            || game.Teardown == null || !game.DimensionsVerified)
        {
            throw new InvalidOperationException("Stability capture or teardown did not finish.");
        }
        File.WriteAllText(options.Output, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, options, measuredPresentedFrames = options.Frames,
            drawableWarmupFrames = DrawableWarmupFrames,
            totalPresentedFrames = game.Presented, actualDrawableVerified = game.DimensionsVerified,
            clock = "integer tick projection of (presented frame+1)/60 seconds; exact24frame400ms pulse",
            configuration =
#if DEBUG
                "Debug",
#else
                "Release",
#endif
            backend = "MonoGame DesktopGL", framework = typeof(Game).Assembly.FullName,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            graphicsProfile = game.GraphicsDevice.GraphicsProfile.ToString(),
            gpuTimeMs = (double?)null,
            gpuTimeUnavailableReason = "MonoGame DesktopGL has no portable GPU timer query API; CPU Present is not GPU execution time.",
            screenshotSha256 = game.ScreenshotHash, frames = game.Samples, pixelHashes = game.PixelHashes,
            sentinelCheckpoints = game.SentinelCheckpoints,
            resourceChecks = new
            {
                game.Fixture.InFlightChecks, game.Fixture.RetirementChecks, game.Fixture.LateMutationChecks,
                game.Fixture.IdleChecks, game.Fixture.MaximumBytes, game.Fixture.MaximumEntries,
                warmBuilds = game.WarmBuilds, warmStatistics = game.WarmStatistics,
                warmBytes = game.WarmBytes, warmEntries = game.WarmEntries,
                latePixelChecks = game.LatePixelChecks, game.Teardown
            }
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    protected override void LoadContent()
    {
        Fixture = new(this, Options);
        World = new(GraphicsDevice);
        Pixel = new(GraphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        if (SimulationFrame < 0)
        {
            return;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        Fixture.Update(SimulationFrame);
        Phase update = Phase.Since(ticks, bytes);
        PendingUpdate = new(PendingUpdate.CpuMs + update.CpuMs, PendingUpdate.AllocatedBytes + update.AllocatedBytes);
        Updates++;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (SimulationFrame < 0)
        {
            DrawWorld();
            return;
        }
        GraphicsMetrics beforePrepare = GraphicsDevice.Metrics;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        Fixture.Desktop.PrepareRenderCaches();
        Phase prepare = Phase.Since(ticks, bytes);
        GraphicsMetrics afterPrepare = GraphicsDevice.Metrics;
        Fixture.AfterPrepare(SimulationFrame);
        GraphicsMetrics before = GraphicsDevice.Metrics;
        bytes = GC.GetAllocatedBytesForCurrentThread();
        ticks = Stopwatch.GetTimestamp();
        DrawWorld();
        Phase world = Phase.Since(ticks, bytes);
        GraphicsMetrics afterWorld = GraphicsDevice.Metrics;
        long allocations = Fixture.Desktop.RenderCaches.Statistics.Allocations;
        bytes = GC.GetAllocatedBytesForCurrentThread();
        ticks = Stopwatch.GetTimestamp();
        Fixture.Desktop.Draw();
        Phase ui = Phase.Since(ticks, bytes);
        GraphicsMetrics afterUi = GraphicsDevice.Metrics;
        if (Updates != 1 || afterUi.TargetCount != afterWorld.TargetCount
            || allocations != Fixture.Desktop.RenderCaches.Statistics.Allocations)
        {
            throw new InvalidOperationException("Expected exactly one Update and no final-UI allocation/target switch.");
        }
        Fixture.AfterPaint(SimulationFrame);
        MGRenderCacheManager manager = Fixture.Desktop.RenderCaches;
        PendingSample = new(SimulationFrame, Fixture.State, PendingUpdate, prepare, world, ui, default,
            Counters.From(afterWorld - before), Counters.From(afterUi - afterWorld), Counters.From(afterUi - before),
            Counters.From(afterPrepare - beforePrepare), afterUi.TextureCount - afterWorld.TextureCount,
            afterPrepare.TextureCount - beforePrepare.TextureCount,
            new(manager.Statistics, manager.RetainedBytes, manager.EntryCount, manager.PendingCount,
                Fixture.DiscoveryVisits, Fixture.SourceReads, manager.MaximumRetainedBytes, manager.MaximumEntries));
        PendingUpdate = default;
        Updates = 0;
        if (SimulationFrame == Options.Warmup - 1)
        {
            WarmBuilds = manager.Statistics.Builds;
            WarmStatistics = manager.Statistics;
            WarmBytes = manager.RetainedBytes;
            WarmEntries = manager.EntryCount;
            Fixture.MarkWarm();
            VerifyBackbuffer();
        }

        // The first real presents precede readback, including on large private Xvfb drawables.
        int index = SimulationFrame - Options.Warmup;
        bool final = SimulationFrame == Options.Warmup + Options.Frames;
        bool soakCheckpoint = Options.Scenario == "resource-soak" && index >= 0 && index < Options.Frames && index % 25 == 0;
        if (Options.VerifyPixels && index >= 0 && index < Options.Frames || final || soakCheckpoint)
        {
            VerifyBackbuffer();
            if (soakCheckpoint)
            {
                SentinelCheckpoints.Add(new { Frame = SimulationFrame,
                    PixelHash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(Pixels.AsSpan()))).ToLowerInvariant() });
            }
            if (Options.VerifyPixels && index >= 0 && index < Options.Frames)
            {
                PixelHashes[index] = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(Pixels.AsSpan()))).ToLowerInvariant();
            }
            if (final)
            {
                using Texture2D capture = new(GraphicsDevice, Options.Width, Options.Height);
                capture.SetData(Pixels);
                using (FileStream output = File.Create(Options.Screenshot))
                {
                    capture.SaveAsPng(output, Options.Width, Options.Height);
                }
                ScreenshotHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Options.Screenshot))).ToLowerInvariant();
            }
        }
    }

    private void DrawWorld()
    {
        GraphicsDevice.Clear(new Color(16, 22, 35));
        World.Begin(blendState: BlendState.Opaque);
        int offset = Presented % 64;
        for (int y = -64; y < Options.Height; y += 64)
        {
            for (int x = -64; x < Options.Width; x += 64)
            {
                World.Draw(Pixel, new Rectangle(x + offset, y, 64, 64), ((x / 64 + y / 64) & 1) == 0
                    ? new Color(42, 67, 91) : new Color(75, 97, 118));
            }
        }
        World.Draw(Pixel, new Rectangle(Options.Width - 8, Options.Height - 8, 4, 4), new Color(205, 41, 77));
        World.Draw(Pixel, new Rectangle(Options.Width - 16, Options.Height - 8, 4, 4), new Color(19, 211, 83));
        World.End();
    }

    private void VerifyBackbuffer()
    {
        PresentationParameters parameters = GraphicsDevice.PresentationParameters;
        if (parameters.BackBufferWidth != Options.Width || parameters.BackBufferHeight != Options.Height
            || GraphicsDevice.Viewport.Width != Options.Width || GraphicsDevice.Viewport.Height != Options.Height)
        {
            throw new InvalidOperationException("Actual native backbuffer/viewport differs from requested dimensions.");
        }
        GraphicsDevice.GetBackBufferData(Pixels);
        if (Fixture.TryGetLatePixel(SimulationFrame, out Point latePosition, out Color lateColor))
        {
            if (!GraphicsDevice.Viewport.Bounds.Contains(latePosition)
                || Pixels[latePosition.Y * Options.Width + latePosition.X] != lateColor)
            {
                throw new InvalidOperationException("Late source generation did not paint the exact changed opaque fill pixel.");
            }
            LatePixelChecks++;
        }
        int worldX = Options.Width - 24;
        int worldY = Options.Height - 12;
        int tile = (int)Math.Floor((worldX - Presented % 64) / 64.0) + worldY / 64;
        Color expected = (tile & 1) == 0 ? new(42, 67, 91) : new(75, 97, 118);
        if (Pixels[worldY * Options.Width + worldX] != expected
            || Pixels[(Options.Height - 7) * Options.Width + Options.Width - 7] != new Color(205, 41, 77)
            || Pixels[(Options.Height - 7) * Options.Width + Options.Width - 15] != new Color(19, 211, 83)
            || !Pixels.Any(value => value.R > 180 && value.G > 180 && value.B > 180))
        {
            throw new InvalidOperationException("Backbuffer lost the moving world or visible UI/text.");
        }
        DimensionsVerified = true;
    }

    protected override void EndDraw()
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Stopwatch.GetTimestamp();
        base.EndDraw();
        Phase present = Phase.Since(ticks, bytes);
        int index = SimulationFrame - Options.Warmup;
        if (index >= 0 && index < Samples.Length)
        {
            Samples[index] = PendingSample with { Present = present };
        }
        Presented++;
        if (Presented == DrawableWarmupFrames + Options.Warmup + Options.Frames + 1)
        {
            Exit();
        }
    }

    protected override void UnloadContent()
    {
        Fixture.Desktop.RenderCaches.Clear();
        Fixture.CheckEmpty();
        Fixture.Dispose();
        MGRenderCacheManager manager = Fixture.Desktop.RenderCaches;
        Teardown = new { manager.IsDisposed, manager.RetainedBytes, Entries = manager.EntryCount,
            Pending = manager.PendingCount, manager.Statistics.Allocations, manager.Statistics.Disposals };
        Pixel.Dispose();
        World.Dispose();
        base.UnloadContent();
    }
}
