using System.Text.Json;
namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public class RenderCachePreparationNativeTests
{
    [Fact]
    public void PreparationPreservesNativeSceneAndLifecycle()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--preparation");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(x => x.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.Equal(16, checks.Count);
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
    }
}
