using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class InteractiveControlNativeTests
{
    [Fact]
    public void BuiltinInteractivePaintHasNativeCaptureParity()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--interactive");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(x => x.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.Equal(17, checks.Count);
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
    }
}
