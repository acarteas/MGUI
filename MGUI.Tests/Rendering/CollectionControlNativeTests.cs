using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class CollectionControlNativeTests
{
    [Fact]
    public void CollectionControlsInvalidateOwnedVisualsAndPreserveNativeCapturePixels()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--collection-controls");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(value => value.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.Equal(12, checks.Count);
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
    }
}
