using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class SampleNativeTests
{
    [Fact]
    public void ActualXamlSampleCommandsModesAnimationAndInvalidationWork()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--sample");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(value => value.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.True(checks.Count >= 10);
        Assert.All(checks, item => Assert.True(item.Value, item.Key));
    }
}
