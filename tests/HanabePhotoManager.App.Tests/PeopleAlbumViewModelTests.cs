using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PeopleAlbumViewModelTests
{
    [Fact]
    public void ToggleBubbles_OpensAndClearSelectionCloses()
    {
        var store = Path.Combine(Path.GetTempPath(), $"people-{Guid.NewGuid():N}.json");
        var viewModel = new PeopleAlbumViewModel(new PeopleAlbumService(store), () => []);

        viewModel.ToggleBubblesCommand.Execute(null);
        viewModel.AreBubblesOpen.Should().BeTrue();
        viewModel.ClearSelectionCommand.Execute(null);
        viewModel.AreBubblesOpen.Should().BeFalse();
    }

    [Fact]
    public void BrowsePeoplePanel_ShowsLiveEngineProgressCountsAndSettingsLocation()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("PeopleAlbums.RecognitionEngineText");
        xaml.Should().Contain("PeopleAlbums.RecognitionDetailsText");
        xaml.Should().Contain("PeopleAlbums.ScanProgressValue");
        xaml.Should().Contain("PeopleAlbums.CancelScanCommand");
        xaml.Should().Contain("设置 → 高级 → 人脸识别");
        xaml.Should().Contain("PeopleAlbums.SummaryText");
        xaml.Should().NotContain("<Ellipse Width=\"62\" Height=\"62\"");
        xaml.Should().Contain("Content=\"返回全部照片\"");
        xaml.Should().Contain("PeopleRecognitionModelPanel");
        xaml.Should().Contain("Background=\"Transparent\"");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
