using System.IO;
using HanabePhotoManager.App.Video;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FfmpegExecutableLocatorTests : IDisposable
{
    private readonly string _fixture = Path.Combine(@"D:\HanabePhoto", ".artifacts", "ffmpeg-locator-tests", Guid.NewGuid().ToString("N"));

    public FfmpegExecutableLocatorTests() => Directory.CreateDirectory(_fixture);

    [Fact]
    public void BundledExecutable_IsFoundWithEmptyPathAndPreferredOverSystemPath()
    {
        var tools = Directory.CreateDirectory(Path.Combine(_fixture, "tools"));
        var bundled = Path.Combine(tools.FullName, "ffmpeg.exe");
        File.WriteAllBytes(bundled, [1]);
        var system = Directory.CreateDirectory(Path.Combine(_fixture, "system"));
        File.WriteAllBytes(Path.Combine(system.FullName, "ffmpeg.exe"), [1]);

        Assert.Equal(bundled, FfmpegExecutableLocator.Find(_fixture, string.Empty));
        Assert.Equal(bundled, FfmpegExecutableLocator.Find(_fixture, system.FullName));
    }

    [Fact]
    public void ExecutableAlongsideApp_IsFoundWithoutLauncher()
    {
        var executable = Path.Combine(_fixture, "ffmpeg.exe");
        File.WriteAllBytes(executable, [1]);
        Assert.Equal(executable, FfmpegExecutableLocator.Find(_fixture, null));
    }

    [Fact]
    public void QuotedSystemPath_IsResolvedWhenPackageDoesNotContainFfmpeg()
    {
        var system = Directory.CreateDirectory(Path.Combine(_fixture, "system tools"));
        var executable = Path.Combine(system.FullName, "ffmpeg.exe");
        File.WriteAllBytes(executable, [1]);
        Assert.Equal(executable, FfmpegExecutableLocator.Find(_fixture, $"\"{system.FullName}\""));
        Assert.Null(FfmpegExecutableLocator.Find(_fixture, string.Empty));
    }

    public void Dispose() => Directory.Delete(_fixture, recursive: true);
}
