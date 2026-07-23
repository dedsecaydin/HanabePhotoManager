using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WindowsWallpaperServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-wallpaper-{Guid.NewGuid():N}");

    [Fact]
    public void GetCurrentWallpaperPath_ReturnsNormalizedExistingPath()
    {
        Directory.CreateDirectory(_directory);
        var wallpaper = Path.Combine(_directory, "wallpaper.jpg");
        File.WriteAllBytes(wallpaper, [1, 2, 3]);
        var service = new WindowsWallpaperService(() => wallpaper);

        service.GetCurrentWallpaperPath().Should().Be(Path.GetFullPath(wallpaper));
    }

    [Fact]
    public void GetCurrentWallpaperPath_ReturnsNullForMissingOrBlankPath()
    {
        new WindowsWallpaperService(() => "  ").GetCurrentWallpaperPath().Should().BeNull();
        new WindowsWallpaperService(() => Path.Combine(_directory, "missing.jpg"))
            .GetCurrentWallpaperPath().Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
