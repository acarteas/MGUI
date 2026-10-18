using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class TextImageNativeTests
{
    [Fact]
    public void TextImagesFontsAndInputRemainCurrent()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--text-images");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(x => x.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.True(checks.Count >= 20, line);
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
    }
}
