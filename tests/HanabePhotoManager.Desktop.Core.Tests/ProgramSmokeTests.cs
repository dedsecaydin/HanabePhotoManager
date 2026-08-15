using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace HanabePhotoManager.Desktop.Core.Tests;

public sealed class ProgramSmokeTests
{
    [Fact]
    public async Task SmokeTestHost_LoadsTheAvaloniaApplicationAndMainWindowWithoutShowing()
    {
        var hostName = OperatingSystem.IsWindows()
            ? "HanabePhotoManager.Desktop.exe"
            : "HanabePhotoManager.Desktop";
        var hostPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HanabePhotoManager.Desktop",
            "bin",
            "Release",
            "net8.0",
            RuntimeInformation.RuntimeIdentifier,
            hostName);
        File.Exists(hostPath).Should().BeTrue("the Desktop project reference should build its self-contained native host");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ArgumentList = { "--smoke-test" }
        });
        process.Should().NotBeNull();

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        var output = await outputTask;
        var error = await errorTask;
        process.ExitCode.Should().Be(0, $"stdout: {output}{Environment.NewLine}stderr: {error}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the test output directory.");
    }
}
