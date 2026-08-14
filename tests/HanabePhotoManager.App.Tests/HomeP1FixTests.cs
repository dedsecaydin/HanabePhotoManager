using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>
/// Regression protection for the 50% Home mid-review P1 fixes:
/// image-first layout (P1-1), adaptive thumbnail wrap (P1-2), video media
/// expression + Home thumbnail loading (P1-3), compact quick actions (P1-6),
/// and the lightweight status summary.
/// </summary>
public sealed class HomeP1FixTests
{
    [Fact]
    public void Home_ThumbnailsAreImageFirstAdaptiveAndVideoBadged()
    {
        var xaml = Read("MainWindow.xaml");

        // P1-2: adaptive wrap, no fixed column count; image-first main visual.
        xaml.Should().Contain("<WrapPanel");
        xaml.Should().Contain("最近照片");
        xaml.Should().NotContain("实时扫描缩略图");
        xaml.Should().Contain("ImageSource=\"{Binding Thumbnail}\"");

        // P1-3: video play indicator + type badge keyed off the extension.
        xaml.Should().Contain("Icon.Play");
        xaml.Should().Contain("Value=\"MP4\"");
        xaml.Should().Contain("Value=\"MOV\"");
    }

    [Fact]
    public void Home_QuickActionsAreCompactToolbarNotCardGrid()
    {
        var xaml = Read("MainWindow.xaml");

        // P1-6: 2x3 card grid demoted to a compact toolbar; no QuickCard style.
        xaml.Should().Contain("快速操作");
        xaml.Should().Contain("Button.Toolbar");
        xaml.Should().NotContain("HomeQuickEntry");
        xaml.Should().NotContain("<UniformGrid Columns=\"3\">");
    }

    [Fact]
    public void Home_SummaryIsALightweightStatusLine()
    {
        var layout = Read("Themes", "Controls", "Layout.xaml");

        layout.Should().Contain("Layout.HomeSummary");
        layout.Should().NotContain("Shadow.Emphasis");
        layout.Should().NotContain("Effect");
    }

    [Fact]
    public void Home_IconPlayTokenIsSharedAcrossThemes()
    {
        var icons = Read("Themes", "Tokens", "Icons.xaml");
        icons.Should().Contain("x:Key=\"Icon.Play\"");
    }

    [Fact]
    public void NavigatingToHome_TriggersHomeThumbnailLoading()
    {
        // P1-3 root cause: the app boots on Preview, so the Home preview tiles
        // never received thumbnails until the CurrentPage setter routed Home to
        // the same thumbnail loader.
        var vm = Read("ViewModels", "MainWindowViewModel.cs");
        vm.Should().Contain("else if (IsHomePage)");
        vm.Should().Contain("StartPreviewThumbnailLoading(HomePreviewFiles)");
    }

    private static string Read(params string[] parts) => File.ReadAllText(
        Path.Combine([SourceRoot(), "src", "HanabePhotoManager.App", .. parts]));

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
