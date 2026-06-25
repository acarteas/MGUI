using System.Diagnostics;
using System.Text.Json;

namespace MGUI.Tests.UI;

public class MGEffectFillBrushRealEffectTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, bool>> Results = new(RunHost);

    [Theory]
    [InlineData("standard-values")]
    [InlineData("callback-last")]
    [InlineData("standard-custom-callback-order")]
    [InlineData("opt-in-default-off")]
    [InlineData("custom-types")]
    [InlineData("missing-parameter")]
    [InlineData("incompatible-diagnostic")]
    [InlineData("shared-aba")]
    [InlineData("cache-invalidation")]
    [InlineData("copy-independent")]
    [InlineData("reusable-binding")]
    public void CompiledEffect_ParameterApplicationContractPasses(string Requirement)
    {
        Assert.True(Results.Value.TryGetValue(Requirement, out bool Passed), $"The live effect host did not report '{Requirement}'.");
        Assert.True(Passed, $"The live effect host reported failure for '{Requirement}'.");
    }

    private static IReadOnlyDictionary<string, bool> RunHost()
    {
        string HostPath = Path.Combine(AppContext.BaseDirectory, "MGUI.EffectTestHost.dll");
        ProcessStartInfo StartInfo = new("dotnet", $"\"{HostPath}\"")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using Process Process = Process.Start(StartInfo)!;
        string Output = Process.StandardOutput.ReadToEnd();
        string Error = Process.StandardError.ReadToEnd();
        Process.WaitForExit();

        string? ResultLine = Output.Split('\n').LastOrDefault(x => x.StartsWith("RESULT:", StringComparison.Ordinal));
        Assert.True(Process.ExitCode == 0 && ResultLine != null,
            $"Live effect host failed with exit code {Process.ExitCode}.\nSTDOUT:\n{Output}\nSTDERR:\n{Error}");
        return JsonSerializer.Deserialize<Dictionary<string, bool>>(ResultLine!["RESULT:".Length..])!;
    }
}
