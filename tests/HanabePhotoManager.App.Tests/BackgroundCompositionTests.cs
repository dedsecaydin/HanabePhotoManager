using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class BackgroundCompositionTests
{
    [Fact]
    public void WindowsWallpaperMode_UsesWallpaperServiceInsteadOfCustomPath()
    {
        var wallpaper = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wallpaper.jpg"));
        var viewModel = new MainWindowViewModel(new StubWallpaperService(wallpaper))
        {
            CustomBackgroundPath = Path.Combine(Path.GetTempPath(), "custom.jpg"),
            BackgroundMode = "跟随 Windows 壁纸"
        };

        viewModel.EffectiveBackgroundPath.Should().Be(wallpaper);
        viewModel.HasEffectiveBackground.Should().BeTrue();
    }

    [Fact]
    public void GlassIntensity_ChangesBothPanelAndOverlayOpacityWithoutHidingWallpaper()
    {
        var viewModel = new MainWindowViewModel(new StubWallpaperService("wallpaper.jpg"));

        viewModel.GlassIntensity = 0.25;
        var lightPanel = viewModel.PanelOpacity;
        var lightOverlay = viewModel.BackgroundOverlayOpacity;
        viewModel.GlassIntensity = 0.95;

        viewModel.PanelOpacity.Should().BeGreaterThan(lightPanel).And.BeLessThan(0.9);
        viewModel.BackgroundOverlayOpacity.Should().BeGreaterThan(lightOverlay).And.BeLessThan(0.7);
    }

    [Fact]
    public void IsAcrylicEnabled_DefaultsToTrueAndTogglesCleanly()
    {
        var viewModel = new MainWindowViewModel(new StubWallpaperService("wallpaper.jpg"));

        viewModel.IsAcrylicEnabled.Should().BeTrue();
        viewModel.IsAcrylicEnabled = false;
        viewModel.IsAcrylicEnabled.Should().BeFalse();
        viewModel.IsAcrylicEnabled = true;
        viewModel.IsAcrylicEnabled.Should().BeTrue();
    }

    private sealed class StubWallpaperService(string? path) : IWindowsWallpaperService
    {
        public string? GetCurrentWallpaperPath() => path;
    }
}
