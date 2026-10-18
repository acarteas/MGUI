using System.Globalization;
using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class RenderCacheMatrixNativeTests
{
    [Theory]
    [InlineData(1280, 720, 1f)]
    [InlineData(1280, 720, 1.5f)]
    [InlineData(1280, 720, 2f)]
    [InlineData(1920, 1080, 1f)]
    [InlineData(1920, 1080, 1.5f)]
    [InlineData(1920, 1080, 2f)]
    public void RequestedPhysicalSurfacePreservesMatchedPixelsPulseInputAndGenerations(int width, int height, float scale)
    {
        string scaleText = scale.ToString(CultureInfo.InvariantCulture);
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--matrix", width.ToString(), height.ToString(), scaleText);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string directory = result.Output.Split('\n').Single(line => line.StartsWith("ARTIFACTS:"))[10..].Trim();
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "report.json")));
        JsonElement root = report.RootElement;
        Assert.Equal(width, root.GetProperty("width").GetInt32());
        Assert.Equal(height, root.GetProperty("height").GetInt32());
        Assert.Equal(scale, root.GetProperty("layoutScale").GetSingle());
        Assert.True(root.GetProperty("genuinePresentedWarmups").GetInt32() >= 3);
        var checks = root.GetProperty("checks").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetBoolean());
        string[] suffixes = ["400ms-no-static-rebuild-exact-pixels-live-updates", "mouse-focus-keyboard-selection",
            "hover-press-selection-disabled-toggle", "wheel-scroll-and-visible-row-input", "scaled-slider-drag",
            "tooltip-and-context-menu", "warm-boundary-control-input-update-and-invalidation",
            "pending-offset-context-and-primary-foreground", "first-miss-content-and-layered-alpha-exact",
            "text-font-foreground-image-mutations", "omitted-prepare-and-late-invalidation",
            "hidden-collapsed-empty-and-recovery", "translated-resized-window",
            "alpha-transform-overflow-direct-fallback", "public-callback-direct-and-recovery"];
        foreach (string suffix in suffixes)
        {
            Assert.Contains(checks, item => item.Key.EndsWith(suffix, StringComparison.Ordinal) && item.Value);
        }
        Assert.All(checks, item => Assert.True(item.Value, item.Key));
        JsonElement[] comparisons = root.GetProperty("comparisons").EnumerateArray().ToArray();
        Assert.True(comparisons.Length >= 180);
        Assert.All(comparisons, comparison =>
        {
            Assert.Equal(0, comparison.GetProperty("different").GetInt32());
            Assert.Equal(comparison.GetProperty("directSha256").GetString(), comparison.GetProperty("sha256").GetString());
            Assert.Equal(TimeSpan.FromDays(1).Ticks,
                comparison.GetProperty("matchedState").GetProperty("caretBlinkRateTicks").GetInt64());
            Assert.True(comparison.GetProperty("matchedClockTicks").GetInt64() > 0);
            Assert.Equal(comparison.GetProperty("matchedClockTicks").GetInt64(),
                comparison.GetProperty("matchedState").GetProperty("clockTicks").GetInt64());
            Assert.All(comparison.GetProperty("maxima").EnumerateArray(), value => Assert.Equal(0, value.GetInt32()));
            Assert.All(comparison.GetProperty("allowedErrorsRGBA").EnumerateArray(), value => Assert.Equal(0, value.GetInt32()));
            foreach (string field in new[] { "directImage", "cachedImage", "differenceImage" })
            {
                string path = comparison.GetProperty(field).GetString()!;
                Assert.True(File.Exists(path), path);
                byte[] header = File.ReadAllBytes(path).Take(24).ToArray();
                Assert.Equal(width, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)));
                Assert.Equal(height, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
            }
        });
    }
}
