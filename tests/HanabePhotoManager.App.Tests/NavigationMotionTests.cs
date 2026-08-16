using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class NavigationMotionTests
{
    private static readonly string[] AllPages =
    [
        "Home", "Import", "Preview", "CustomAlbums", "FaceSearch", "MapPhotos",
        "Compression", "Watermark", "Settings"
    ];

    [Fact]
    public void PageTransition_MapsEveryNavigationDestinationToItsHost()
    {
        var code = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        foreach (var page in AllPages)
        {
            code.Should().Contain($"\"{page}\" =>", $"the {page} destination must resolve to a page host");
        }

        // The previously broken mappings are now explicit.
        code.Should().Contain("\"Settings\" => SettingsCenterPageHost");
        code.Should().Contain("\"CustomAlbums\" => CustomAlbumsPageHost");
        code.Should().Contain("\"Watermark\" => WatermarkPageHost");

        // The deprecated collapsed ScrollViewer must no longer be the animation target.
        code.Should().NotContain("\"Settings\" => SettingsPage");
    }

    [Fact]
    public void SidebarNavigation_ShowsAnimatedSelectedStateAndKeyboardNavigation()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("x:Name=\"PrimaryNavigationSelectionIndicator\"");
        xaml.Should().Contain("x:Name=\"PrimaryNavigationSelectionTransform\"");
        xaml.Should().NotContain("x:Name=\"NavSelectionSurface\"");
        xaml.Should().Contain("x:Name=\"NavIconSurface\"");
        xaml.Should().Contain("Source=\"{DynamicResource Image.AppLogo}\"");
        xaml.Should().Contain("Motion.Duration.Normal");
        xaml.Should().Contain("KeyboardNavigation.DirectionalNavigation=\"Cycle\"");
        xaml.Should().Contain("KeyboardNavigation.TabNavigation=\"Once\"");

        var code = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));
        code.Should().Contain("UpdatePrimaryNavigationIndicator");
        code.Should().Contain("TranslateTransform.YProperty");
        code.Should().Contain("Motion.Easing.Standard");
        code.Should().Contain("ResetGalleryScrollToFirstDate");
        code.Should().Contain("GetGalleryPanel()?.SetVerticalOffset(0)");
    }

    [Fact]
    public void KeyboardShortcut_CtrlF_FocusesBrowseSearch()
    {
        var code = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        code.Should().Contain("FocusBrowseSearch");
        code.Should().Contain("Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)");
        code.Should().Contain("BrowseSmartSearchBox.Focus()");
    }

    [Fact]
    public void RapidPageSwitching_AlwaysActivatesExactlyOnePage()
    {
        var viewModel = new MainWindowViewModel();

        for (var round = 0; round < 20; round++)
        {
            foreach (var page in AllPages)
            {
                viewModel.CurrentPage = page;

                viewModel.CurrentPage.Should().Be(page);
                foreach (var other in AllPages)
                {
                    IsPageActive(viewModel, other)
                        .Should().Be(other == page, $"after switching to {page}, {other} has an unexpected active state");
                }
            }
        }
    }

    [Fact]
    public void RapidPageSwitching_DoesNotRecreateChildViewModels()
    {
        var viewModel = new MainWindowViewModel();
        var mapPhotos = viewModel.MapPhotos;
        var compression = viewModel.Compression;
        var watermark = viewModel.Watermark;
        var customAlbums = viewModel.CustomAlbums;
        var photoViewer = viewModel.PhotoViewer;
        var treemap = viewModel.TreemapBrowser;

        for (var round = 0; round < 50; round++)
        {
            foreach (var page in AllPages)
            {
                viewModel.CurrentPage = page;
            }
        }

        viewModel.MapPhotos.Should().BeSameAs(mapPhotos);
        viewModel.Compression.Should().BeSameAs(compression);
        viewModel.Watermark.Should().BeSameAs(watermark);
        viewModel.CustomAlbums.Should().BeSameAs(customAlbums);
        viewModel.PhotoViewer.Should().BeSameAs(photoViewer);
        viewModel.TreemapBrowser.Should().BeSameAs(treemap);
    }

    private static bool IsPageActive(MainWindowViewModel viewModel, string page) => page switch
    {
        "Home" => viewModel.IsHomePage,
        "Import" => viewModel.IsImportPage,
        "Preview" => viewModel.IsPreviewPage,
        "CustomAlbums" => viewModel.IsCustomAlbumsPage,
        "FaceSearch" => viewModel.IsFaceSearchPage,
        "MapPhotos" => viewModel.IsMapPhotosPage,
        "Compression" => viewModel.IsCompressionPage,
        "Watermark" => viewModel.IsWatermarkPage,
        "Settings" => viewModel.IsSettingsPage,
        _ => false
    };

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
