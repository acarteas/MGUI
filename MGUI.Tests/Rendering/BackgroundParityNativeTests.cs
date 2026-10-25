using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class BackgroundParityNativeTests
{
    [Fact]
    public void LayeredBackgroundCachePreservesNativePixelsAndResourceLifecycle()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--background-parity");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(result.Output.Split('\n').Single(x => x.StartsWith("RESULT:"))[7..])!;
        string[] required = ["owner-site-size-scale-translation-target-isolation", "partial-clip-layered-direct-parity",
            "opaque-effect-texture-list-invalidation-before-and-after-prepare", "nested-color-dependency-invalidates",
            "source-mode-copy-and-known-volatility-isolation", "fractional-opacity-effect-blend-sampler-transform-fallback",
            "moving-world-sentinel-preserved", "warm-idle-build-and-allocation-stop", "sampled-target-retirement-waits-for-safe-point",
            "entry-byte-dimension-budgets-evict-and-recover", "native-target-failure-disposal-suppression-and-direct-parity",
            "native-reset-releases-and-rebuilds-brush-targets", "desktop-teardown-disposes-all-owned-targets",
            "caller-texture-effect-device-survive-desktop-disposal", "transparent-and-opaque-scene-layered-parity",
            "effect-bearing-nine-slice-uv-lighting-and-manual-invalidation",
            "unsupported-context-never-queues-before-reference-toggle", "layer-omission-controls-exceed-quantization-allowances",
            "native-device-teardown-completes", "geometry-replacement-retires-targets-before-reference-toggle"];
        foreach (string name in required) { Assert.True(checks.TryGetValue(name, out bool passed) && passed, name); }
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
        var diffs = JsonSerializer.Deserialize<JsonElement[]>(result.Output.Split('\n').Single(x => x.StartsWith("DIFFS:"))[6..])!;
        Assert.True(diffs.Length >= 40, "Expected named native A/B matrix.");
        Assert.All(diffs, diff =>
        {
            string name = diff.GetProperty("Name").GetString()!;
            int allowance = diff.GetProperty("AllowedChannelError").GetInt32();
            bool gradient = name is "effect-bearing-nine-slice-local-uv-scale-1.5-warm" or "effect-bearing-nine-slice-local-uv-scale-2-warm";
            bool translucent = name == "transparent-layers-over-opaque-scene";
            Assert.Equal(gradient || translucent ? 1 : 0, allowance);
            int[] limits = gradient ? [1, 1, 0, 0] : translucent ? [1, 1, 1, 0] : [0, 0, 0, 0];
            Assert.Equal(limits, diff.GetProperty("AllowedErrorsRGBA").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            int[] maxima = diff.GetProperty("ChannelMaximaRGBA").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            for (int channel = 0; channel < 4; channel++) { Assert.InRange(maxima[channel], 0, limits[channel]); }
            Assert.InRange(diff.GetProperty("MaximumChannelError").GetInt32(), 0, allowance);
            Assert.True(File.Exists(diff.GetProperty("NormalImage").GetString()));
            Assert.True(File.Exists(diff.GetProperty("CachedImage").GetString()));
        });
        string report = result.Output.Split('\n').Single(x => x.StartsWith("DIFFREPORT:"))[11..].Trim();
        Assert.True(File.Exists(report), "Native image diff report was not retained: " + report);
    }
}
