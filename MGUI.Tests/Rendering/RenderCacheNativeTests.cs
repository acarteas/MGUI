using System.Text.Json;

namespace MGUI.Tests.Rendering;

[Collection("Render cache clock")]
public class RenderCacheNativeTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, bool>> Results = new(RunHost);

    [Theory]
    [InlineData("native-settings-first-bind-readback")]
    [InlineData("caller-resource-ownership")]
    [InlineData("unused-and-queued-cpu-only")]
    [InlineData("reentrant-clear-sampled-bound-safe")]
    [InlineData("reentrant-dispose-sampled-bound-safe")]
    [InlineData("native-clear-callback-drains-all")]
    [InlineData("native-dispose-callback-drains-all")]
    [InlineData("native-first-bind-failure-release-suppression")]
    [InlineData("native-device-reset-forwarding")]
    [InlineData("desktop-owned-same-service-permanent-disposal")]
    [InlineData("live-device-weak-manager-and-owner")]
    [InlineData("native-multiple-devices-isolated-and-device-disposal")]
    [InlineData("all-native-targets-released-once")]
    public void RealNativeResourcesMeetContract(string requirement)
    {
        Assert.True(Results.Value.TryGetValue(requirement, out bool passed), $"Resource host did not report {requirement}.");
        Assert.True(passed);
    }
    private static IReadOnlyDictionary<string, bool> RunHost()
    {
        var (exitCode, output, error) = NativeGraphicsHost.Run("MGUI.RenderCacheTestHost.dll", "--resources");
        string? result = output.Split('\n').LastOrDefault(x => x.StartsWith("RESULT:", StringComparison.Ordinal));
        Assert.True(exitCode == 0 && result != null, $"Resource host exit {exitCode}.\n{output}\n{error}");
        return JsonSerializer.Deserialize<Dictionary<string, bool>>(result!["RESULT:".Length..])!;
    }
}
