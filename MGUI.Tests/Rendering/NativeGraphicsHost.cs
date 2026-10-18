using System.Diagnostics;

namespace MGUI.Tests.Rendering;

// Linux graphics hosts own their display and shared graphics lock in one child process.
// The parent test/build process must not hold that graphics lock; no child builds while holding it.
internal static class NativeGraphicsHost
{
    internal static (int ExitCode, string Output, string Error) Run(string hostFileName, params string[] arguments)
    {
        ProcessStartInfo start = new(OperatingSystem.IsLinux() ? "python3" : "dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (OperatingSystem.IsLinux())
        {
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "test-native.py"));
        }
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, hostFileName));
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Owned native graphics host did not finish.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
