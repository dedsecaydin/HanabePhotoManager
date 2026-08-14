using FluentAssertions;
using HanabePhotoManager.App.Browsing.Treemap;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Browsing.Treemap;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.Browsing.Treemap;

public sealed class BrowseTreemapIntegrationTests
{
    [Fact]
    public void BrowsePage_KeepsGridAndAddsTreeMapSurface()
    {
        var xaml = File.ReadAllText(ProjectFile("src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("xmlns:treemap=\"clr-namespace:HanabePhotoManager.App.Browsing.Treemap\"");
        xaml.Should().Contain("x:Name=\"BrowseDisplayModeSelector\"");
        xaml.Should().Contain("ItemsSource=\"{Binding PreviewWallItems}\"");
        xaml.Should().Contain("<treemap:PhotoTreemapControl");
        xaml.Should().Contain("ItemsSource=\"{Binding TreemapBrowser.Items}\"");
        xaml.Should().Contain("SelectedPath=\"{Binding SelectedTreemapPath, Mode=TwoWay}\"");
        xaml.Should().Contain("ZoomScale=\"{Binding TreemapZoom}\"");
        xaml.Should().Contain("Text=\"{Binding Label}\"");
        xaml.Should().Contain("Visibility=\"{Binding IsGridBrowseMode, Converter={StaticResource BoolToVis}}\"");
        xaml.Should().Contain("Visibility=\"{Binding IsTreemapBrowseMode, Converter={StaticResource BoolToVis}}\"");
    }

    [Fact]
    public void SwitchingToTreemap_RehydratesAlreadyLoadedPreviewFiles()
    {
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("EnsureTreemapPopulatedFromPreviewFiles()");
        source.Should().Contain("TreemapBrowser.Items.Count > 0");
    }

    [Fact]
    public void IncrementalScan_SeedsViewportThumbnailSourceForInitialWall()
    {
        // The all-library / date scan populates the treemap through ApplyBatch,
        // which never routes through RepopulateTreemapFrom. The viewport queue
        // must still be seeded so the initial photo wall loads thumbnails.
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("EnsureTreemapSourceLookup");
        source.Should().Contain("StartTreemapThumbnailLoading(_filteredCache.ToArray())");
        source.Should().Contain("TreemapRepopulated?.Invoke()");
    }

    [Fact]
    public void ViewModel_DefaultsToGridAndSupportsBothWeightModes()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.BrowseDisplayMode.Should().Be(BrowseDisplayMode.Grid);
        viewModel.IsGridBrowseMode.Should().BeTrue();
        viewModel.TreemapBrowser.WeightMode = TreemapWeightMode.PhotoCount;
        viewModel.TreemapBrowser.WeightMode.Should().Be(TreemapWeightMode.PhotoCount);
    }

    [Fact]
    public void DateScan_ForwardsBatchesAndCompletionToTreemapModule()
    {
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("TreemapBrowser.BeginScan(node.FullPath)");
        source.Should().Contain("TreemapBrowser.ApplyBatch(treemapGeneration, batch)");
        source.Should().Contain("TreemapBrowser.Complete(treemapGeneration, snapshot.IsPartial)");
    }

    [Fact]
    public void RootLibraryScan_AlsoStreamsBatchesToTreemapModule()
    {
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("TreemapBrowser.BeginScan(root)");
        source.Should().Contain("AddPreviewMetadataBatch(ready, scanned, scanVersion, treemapGeneration)");
        source.Should().Contain("TreemapBrowser.Complete(treemapGeneration, isPartial: false)");
    }

    [Fact]
    public void Settings_DefaultToTheAllLibraryGrid()
    {
        var settings = new AppSettings();

        settings.BrowseDisplayMode.Should().Be(nameof(BrowseDisplayMode.Grid));
        settings.TreemapWeightMode.Should().Be(nameof(TreemapWeightMode.FileSize));
    }

    [Fact]
    public void Startup_EntersPreviewWithNeutralAllLibraryFiltersBeforeRootScan()
    {
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("private string _currentPage = \"Preview\"");
        source.Should().Contain("BrowseDisplayMode = BrowseDisplayMode.Grid;");
        source.Should().Contain("PrepareStartupAllLibraryTreemap();");
        source.Should().Contain("_selectedFileTypeFilters.Clear();");
    }

    [Fact]
    public void FilteringTheDefaultTreemapWithoutALibraryRoot_DoesNotStartAScan()
    {
        var viewModel = new MainWindowViewModel();

        var act = () => viewModel.CurrentPreviewCategory = "JPG生图";

        act.Should().NotThrow();
    }

    [Fact]
    public void TreemapSizing_UsesViewportScaledPanoramaAtMinimumZoom()
    {
        var source = File.ReadAllText(ProjectFile(
            "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        source.Should().Contain("GetPanoramaLayout(TreemapScrollViewer.ViewportWidth)");
        source.Should().Contain("HorizontalOffset / zoom");
        source.Should().Contain("ViewportWidth / zoom");
    }

    private static string ProjectFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HanabePhotoManager.sln")))
        {
            current = current.Parent;
        }

        current.Should().NotBeNull("tests must run from inside the repository");
        return Path.Combine([current!.FullName, .. segments]);
    }
}
