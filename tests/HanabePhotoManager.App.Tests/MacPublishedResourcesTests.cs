using System.Diagnostics;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacPublishedResourcesTests
{
    [Fact]
    public void DetectsMissingAndChangedManagedResources()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "publish-resources-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
        try
        {
            File.WriteAllText(Path.Combine(source, "HanabePhotoManager.dll"), "managed");
            File.WriteAllText(Path.Combine(source, "HanabePhotoManager.runtimeconfig.json"), "config");
            Assert.NotEqual(0, Run(source, destination));
            foreach (var path in Directory.GetFiles(source)) File.Copy(path, Path.Combine(destination, Path.GetFileName(path)));
            var verified = Run(source, destination);
            Assert.True(verified == 0, $"Resource verifier returned {verified}; {LastError}");
            File.WriteAllText(Path.Combine(destination, "HanabePhotoManager.dll"), "changed");
            Assert.NotEqual(0, Run(source, destination));
        }
        finally { TemporaryDirectoryCleanup.Delete(root); }
    }

    private static string LastError = string.Empty;
    private static int Run(string source, string destination)
    {
        var script = @"D:\HanabePhoto\src\HanabePhotoManager.Mac\Packaging\Test-PublishedResources.ps1";
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var value in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Publish", source, "-Target", destination }) info.ArgumentList.Add(value);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(); Task.WaitAll(output, error);
        LastError = error.Result + output.Result;
        return process.ExitCode;
    }
}
