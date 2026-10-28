using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public sealed class PasswordInputNativeTests
{
    [Fact]
    public void PasswordInputHistoryCaptureAndCleanupAreSecretSafe()
    {
        var result = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--password-input");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        string line = result.Output.Split('\n').Single(value => value.StartsWith("RESULT:"));
        var checks = JsonSerializer.Deserialize<Dictionary<string, bool>>(line[7..])!;
        Assert.Equal(10, checks.Count);
        Assert.All(checks, check => Assert.True(check.Value, check.Key));
    }
}
