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
    public void BrowsePeoplePanel_ShowsScanAndShowAllPeopleWithoutModelPanel()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("PeopleAlbums.ScanCommand");
        xaml.Should().Contain("PeopleAlbums.ScanProgressValue");
        xaml.Should().Contain("PeopleAlbums.CancelScanCommand");
        xaml.Should().Contain("PeopleAlbums.SummaryText");
        xaml.Should().Contain("PeopleAlbums.ToggleBubblesCommand");
        xaml.Should().NotContain("<Ellipse Width=\"62\" Height=\"62\"");
        xaml.Should().Contain("Content=\"返回全部照片\"");
        xaml.Should().NotContain("PeopleRecognitionModelPanel");
    }

    [Fact]
    public async Task MergeCommand_MergesSelectedPersonIntoChosenTargetAndRefreshesList()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hanabe-people-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var paths = new[] { "alice.jpg", "bob.jpg" }.Select(name =>
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, name);
            return path;
        }).ToArray();
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0],
            [paths[1]] = [0, 1]
        });
        var service = new PeopleAlbumService(Path.Combine(directory, "people.json"), embeddings);
        await service.ScanAsync(paths, default);

        var viewModel = new PeopleAlbumViewModel(
            service,
            () => [],
            candidates => candidates.First());

        await viewModel.InitializeAsync();
        viewModel.Albums.Should().HaveCount(2);

        var source = viewModel.Albums.First(album => album.Name == "A");
        viewModel.SelectedAlbum = source;
        viewModel.MergeCommand.CanExecute(null).Should().BeTrue();

        await viewModel.MergeCommand.ExecuteAsync(null);

        viewModel.Albums.Should().ContainSingle();
        viewModel.Albums[0].PhotoPaths.Should().Contain(paths[0]).And.Contain(paths[1]);
        viewModel.SelectedAlbum.Should().BeSameAs(viewModel.Albums[0]);
        viewModel.StatusText.Should().Contain("合并");
    }

    [Fact]
    public async Task MergeCommand_IsDisabledWithoutASelectionOrWithOnlyOnePerson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hanabe-people-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "only.jpg");
        File.WriteAllText(path, "only");
        var service = new PeopleAlbumService(
            Path.Combine(directory, "people.json"),
            new FakeEmbeddingService(new Dictionary<string, float[]> { [path] = [1, 0] }));
        await service.ScanAsync([path], default);

        var viewModel = new PeopleAlbumViewModel(service, () => [], candidates => candidates.First());
        await viewModel.InitializeAsync();

        viewModel.MergeCommand.CanExecute(null).Should().BeFalse();

        viewModel.SelectedAlbum = viewModel.Albums.First();
        viewModel.MergeCommand.CanExecute(null).Should().BeFalse();
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class FakeEmbeddingService(
        IReadOnlyDictionary<string, float[]> embeddings,
        FaceModelIdentity? identity = null) : ILocalFaceEmbeddingService
    {
        public FaceModelIdentity ModelIdentity { get; } = identity ?? FaceModelIdentity.YuNetSFaceLegacy;

        public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DetectedFace> result = embeddings.TryGetValue(path, out var embedding)
                ? [new DetectedFace(path, embedding, 0, 0, 10, 10)]
                : [];
            return Task.FromResult(result);
        }
    }
}
