using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class CompositionNativeTests
{
    [Fact]
    public void ContentCompositionPreservesNativePainterAlphaClipAndContextSemantics()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--composition");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string directory = result.Output.Split('\n').Single(line => line.StartsWith("ARTIFACTS:"))[10..].Trim();
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "report.json")));
        var checks = report.RootElement.GetProperty("checks");
        string[] required = ["component-painter-order", "phase-omission-controls", "wrapper-and-sibling-painter-order",
            "per-primitive-descendant-and-inherited-alpha", "unsupported-context-reasons-and-direct-parity",
            "offset-full-partial-clip-and-overflow", "main-preview-main-and-alternate-descendant-observations",
            "public-callbacks-never-prepare-and-recover", "scroll-clip-mapping-and-live-bookkeeping",
            "layout-scale-channels-override-and-inheritance", "position-dependent-effects-stay-live",
            "desktop-rasterizer-provenance-including-brush", "live-begin-callback-rasterizer-provenance-and-recovery", "rasterizer-context-survives-opacity-copies"];
        foreach (string name in required) { Assert.True(checks.GetProperty(name).GetBoolean(), name); }
        var comparisons = report.RootElement.GetProperty("comparisons").EnumerateArray().ToArray();
        Assert.True(comparisons.Length >= 30);
        foreach (var comparison in comparisons)
        {
            string name = comparison.GetProperty("name").GetString()!;
            int allowance = name == "descendant-alpha-opaque-scene" ? 1 : 0;
            int[] maxima = comparison.GetProperty("maxima").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Assert.Equal(new[] { allowance, allowance, allowance, 0 }, comparison.GetProperty("allowed").EnumerateArray().Select(x => x.GetInt32()));
            for (int channel = 0; channel < 4; channel++) { Assert.InRange(maxima[channel], 0, channel == 3 ? 0 : allowance); }
            Assert.True(File.Exists(Path.Combine(directory, name + "-direct.png")));
            Assert.True(File.Exists(Path.Combine(directory, name + "-cached.png")));
        }
    }
}
