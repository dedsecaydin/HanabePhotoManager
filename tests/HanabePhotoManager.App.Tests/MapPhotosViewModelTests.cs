using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MapPhotosViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-map-{Guid.NewGuid():N}");

    [Fact]
    public async Task RefreshAsync_SeparatesLocatedAndUnlocatedPhotos()
    {
        var paths = CreateFiles("located.jpg", "none.jpg");
        var store = CreateStore();
        await store.UpsertAsync(new MediaMetadataEntry
        {
            Path = paths[0], ExifLocation = new PhotoLocation(36, 120, PhotoLocationSource.Exif)
        });
        var viewModel = new MapPhotosViewModel(store, () => paths, new StubExifReader());

        await viewModel.RefreshAsync();

        viewModel.LocatedPhotos.Should().ContainSingle();
        viewModel.UnlocatedPhotos.Should().ContainSingle(item => item.Path == paths[1]);
        viewModel.Markers.Should().ContainSingle(marker => marker.Latitude == 36 && marker.Count == 1);
    }

    [Fact]
    public async Task AssignAndClearSelected_UpdatesEffectiveSource()
    {
        var path = CreateFiles("none.jpg").Single();
        var store = CreateStore();
        var viewModel = new MapPhotosViewModel(store, () => [path], new StubExifReader());
        await viewModel.RefreshAsync();
        viewModel.UnlocatedPhotos[0].IsSelected = true;
        viewModel.PendingLatitude = "31.23";
        viewModel.PendingLongitude = "121.47";

        await viewModel.AssignSelectedAsync();
        viewModel.LocatedPhotos.Single().Source.Should().Be(PhotoLocationSource.Manual);
        viewModel.LocatedPhotos[0].IsSelected = true;
        await viewModel.ClearSelectedManualAsync();

        viewModel.UnlocatedPhotos.Should().ContainSingle();
    }

    [Fact]
    public void MarkerPayload_ContainsNoLocalMediaIdentity()
    {
        var marker = new MapMarkerPayload("m1", 10, 20, 3);
        var json = System.Text.Json.JsonSerializer.Serialize(marker);
        json.Should().NotContain("path").And.NotContain("name").And.NotContain("tag").And.NotContain("people");
    }

    [Fact]
    public async Task AddSourcesAsync_RecursivelyIndexesExternalFolderAndPersistsIt()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_directory, "external", "nested")).FullName;
        var path = Path.Combine(nested, "outside.jpg");
        File.WriteAllText(path, "outside");
        var store = CreateStore();
        var viewModel = new MapPhotosViewModel(store, () => Array.Empty<string>(), new StubExifReader());

        await viewModel.AddSourcesAsync([Path.Combine(_directory, "external")], recursive: true);

        viewModel.UnlocatedPhotos.Should().ContainSingle(item => item.Path == Path.GetFullPath(path));
        var snapshot = await store.LoadAsync();
        snapshot.MapSourcePaths.Should().ContainSingle(item => item == Path.GetFullPath(path));
    }

    [Fact]
    public async Task SelectMarker_ExposesOnlyPhotosInThatLocationCluster()
    {
        var paths = CreateFiles("a.jpg", "b.jpg", "far.jpg");
        var store = CreateStore();
        await store.UpsertAsync(new MediaMetadataEntry { Path = paths[0], ExifLocation = new PhotoLocation(31.2301, 121.4701, PhotoLocationSource.Exif) });
        await store.UpsertAsync(new MediaMetadataEntry { Path = paths[1], ExifLocation = new PhotoLocation(31.2302, 121.4702, PhotoLocationSource.Exif) });
        await store.UpsertAsync(new MediaMetadataEntry { Path = paths[2], ExifLocation = new PhotoLocation(39.9, 116.4, PhotoLocationSource.Exif) });
        var viewModel = new MapPhotosViewModel(store, () => paths, new StubExifReader());
        await viewModel.RefreshAsync();

        var marker = viewModel.Markers.Single(item => item.Count == 2);
        viewModel.SelectMarker(marker.Id);

        viewModel.SelectedLocationPhotos.Select(item => item.Path).Should().BeEquivalentTo(paths.Take(2));
        viewModel.GetMarkerPhotoPaths(marker.Id, 1).Should().ContainSingle().Which.Should().Be(paths[0]);
    }

    [Fact]
    public async Task ManualSelectionCommands_SelectInvertAndClearVisibleItems()
    {
        var paths = CreateFiles("a.jpg", "b.jpg", "c.jpg");
        var viewModel = new MapPhotosViewModel(CreateStore(), () => paths, new StubExifReader());
        await viewModel.RefreshAsync();

        viewModel.SelectAllUnlocated();
        viewModel.UnlocatedPhotos.Should().OnlyContain(item => item.IsSelected);
        viewModel.UnlocatedPhotos[0].IsSelected = false;
        viewModel.InvertUnlocatedSelection();
        viewModel.UnlocatedPhotos[0].IsSelected.Should().BeTrue();
        viewModel.UnlocatedPhotos.Skip(1).Should().OnlyContain(item => !item.IsSelected);
        viewModel.ClearSelection();
        viewModel.UnlocatedPhotos.Should().OnlyContain(item => !item.IsSelected);
    }

    [Fact]
    public async Task RemoveSelectedSourcesAsync_RemovesOnlySelectedImportedPhotosWithoutDeletingFiles()
    {
        var paths = CreateFiles("keep.jpg", "remove.jpg");
        var store = CreateStore();
        var viewModel = new MapPhotosViewModel(store, () => Array.Empty<string>(), new StubExifReader());
        await viewModel.AddSourcesAsync(paths, recursive: false);
        viewModel.UnlocatedPhotos.Single(item => item.Path == paths[1]).IsSelected = true;

        await viewModel.RemoveSelectedSourcesAsync();

        viewModel.UnlocatedPhotos.Should().ContainSingle(item => item.Path == paths[0]);
        File.Exists(paths[1]).Should().BeTrue();
        (await store.LoadAsync()).MapSourcePaths.Should().ContainSingle(item => item == paths[0]);
    }

    [Fact]
    public async Task ClearImportedSourcesAsync_EmptiesTheImportedMapWorkspaceWithoutDeletingFiles()
    {
        var paths = CreateFiles("a.jpg", "b.jpg");
        var store = CreateStore();
        var viewModel = new MapPhotosViewModel(store, () => Array.Empty<string>(), new StubExifReader());
        await viewModel.AddSourcesAsync(paths, recursive: false);

        await viewModel.ClearImportedSourcesAsync();

        viewModel.UnlocatedPhotos.Should().BeEmpty();
        paths.Should().OnlyContain(path => File.Exists(path));
        (await store.LoadAsync()).MapSourcePaths.Should().BeEmpty();
    }

    private string[] CreateFiles(params string[] names)
    {
        Directory.CreateDirectory(_directory);
        return names.Select(name =>
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, name);
            return path;
        }).ToArray();
    }
    private MediaMetadataStore CreateStore() => new(Path.Combine(_directory, "metadata.json"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    private sealed class StubExifReader : IExifLocationReader
    {
        public PhotoCoordinate? TryRead(string path) => null;
    }
}
