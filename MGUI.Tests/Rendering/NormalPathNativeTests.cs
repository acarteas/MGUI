using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class NormalPathNativeTests
{
    [Fact]
    public void UnusedAndDisabledCachingPreservePixelsBatchingUpdateAndIdleObservations()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--normal-guards");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string path = result.Output.Split('\n').Single(line => line.StartsWith("REPORT:"))[7..].Trim();
        Assert.True(File.Exists(path));
        using JsonDocument report = JsonDocument.Parse(result.Output.Split('\n').Single(line => line.StartsWith("RESULT:"))[7..]);
        Assert.Equal(144, report.RootElement.GetProperty("exactComparisons").GetInt32());
        Assert.Equal(6, report.RootElement.GetProperty("idle").GetArrayLength());
        Assert.Equal(100, report.RootElement.GetProperty("stableBookkeepingFrames").GetInt32());
        Assert.Equal(34, report.RootElement.GetProperty("inputFrames").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("revisionBytes").GetInt64());
        Assert.Equal(0, report.RootElement.GetProperty("emptyPreparationBytes").GetInt64());
        Assert.Equal(report.RootElement.GetProperty("ordinaryWrapperDrawBytes").EnumerateArray().Min(value => value.GetInt64()),
            report.RootElement.GetProperty("disabledWrapperDrawBytes").EnumerateArray().Min(value => value.GetInt64()));
        foreach (var idle in report.RootElement.GetProperty("idle").EnumerateArray())
        {
            foreach (string counter in new[] { "discoveryVisits", "sourceReads", "observerSlots", "subscriptionChanges" })
            { Assert.Equal(0, idle.GetProperty(counter).GetInt32()); }
        }
    }
}
