using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PeopleAlbumServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-people-{Guid.NewGuid():N}");

    [Fact]
    public async Task ScanAsync_CreatesAlphabeticalAlbumsAndKeepsIdentityAcrossRestart()
    {
        var paths = CreateFiles("alice-1.jpg", "bob-1.jpg", "alice-2.jpg");
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0, 0], [paths[1]] = [0, 1, 0], [paths[2]] = [0.99f, 0.02f, 0]
        });
        var storePath = Path.Combine(_directory, "people.json");
        var service = new PeopleAlbumService(storePath, embeddings);

        var first = await service.ScanAsync(paths, default);
        first.Albums.Select(album => album.Name).Should().Equal("A", "B");
        first.Albums.Single(album => album.Name == "A").PhotoPaths.Should().HaveCount(2);

        var restarted = new PeopleAlbumService(storePath, embeddings);
        var second = await restarted.ScanAsync(paths, default);
        second.Albums.Select(album => album.Id).Should().BeEquivalentTo(first.Albums.Select(album => album.Id));
    }

    [Fact]
    public async Task CurrentSFaceThreshold_MergesTheSamePersonAcrossPoseVariation()
    {
        var paths = CreateFiles("front.jpg", "side.jpg");
        var identity = FaceModelIdentity.YuNetSFaceCurrent;
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0],
            [paths[1]] = [0.5f, 0.8660254f]
        }, identity);

        var result = await new PeopleAlbumService(
            Path.Combine(_directory, "pose.json"), embeddings).ScanAsync(paths, default);

        result.Albums.Should().ContainSingle();
        result.Albums[0].PhotoPaths.Should().HaveCount(2);
        result.Albums[0].MatchCentroids.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_UsesTheDetectedFaceCropAsTheAlbumCover()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "portrait.jpg");
        using (var image = new Image<Rgb24>(100, 100, new Rgb24(20, 40, 60)))
            await image.SaveAsJpegAsync(path);
        var service = new PeopleAlbumService(
            Path.Combine(_directory, "crop.json"),
            new FakeEmbeddingService(new Dictionary<string, float[]> { [path] = [1, 0] }));

        var result = await service.ScanAsync([path], default);

        result.Albums.Should().ContainSingle();
        result.Albums[0].CoverPath.Should().NotBe(path);
        File.Exists(result.Albums[0].CoverPath).Should().BeTrue();
    }

    [Fact]
    public async Task RenameMergeAndRemoval_AreDurableManualOverrides()
    {
        var paths = CreateFiles("a.jpg", "b.jpg", "a2.jpg");
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0], [paths[1]] = [0, 1], [paths[2]] = [0.98f, 0.02f]
        });
        var storePath = Path.Combine(_directory, "people.json");
        var service = new PeopleAlbumService(storePath, embeddings);
        var initial = await service.ScanAsync(paths, default);
        var first = initial.Albums[0];
        var second = initial.Albums[1];

        await service.RenameAsync(first.Id, "小花", default);
        await service.RemovePhotoAsync(first.Id, paths[2], default);
        await service.MergeAsync(first.Id, second.Id, default);

        var restarted = new PeopleAlbumService(storePath, embeddings);
        var rescanned = await restarted.ScanAsync(paths, default);
        rescanned.Albums.Should().ContainSingle();
        rescanned.Albums[0].Name.Should().Be("小花");
        rescanned.Albums[0].PhotoPaths.Should().Contain(paths[0]).And.Contain(paths[1]).And.NotContain(paths[2]);
    }

    [Fact]
    public async Task ScanAsync_UpdatingASubsetKeepsPeopleFromEarlierPhotos()
    {
        var paths = CreateFiles("first.jpg", "second.jpg");
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0], [paths[1]] = [0, 1]
        });
        var service = new PeopleAlbumService(Path.Combine(_directory, "people.json"), embeddings);
        await service.ScanAsync(paths, default);

        var updated = await service.ScanAsync([paths[0]], default);

        updated.Albums.SelectMany(album => album.PhotoPaths).Should().Contain(paths);
    }

    [Fact]
    public async Task LegacyStore_MigratesOnlyForCompatibleYuNetIdentity()
    {
        var paths = CreateFiles("legacy.jpg");
        var storePath = Path.Combine(_directory, "people.json");
        await File.WriteAllTextAsync(storePath,
            """{"Version":1,"Albums":[{"Id":"legacy","Name":"A","PhotoPaths":[],"MatchCentroids":[[1,0]]}],"RemovedPhotos":{}}""");
        var embeddings = new FakeEmbeddingService(
            new Dictionary<string, float[]> { [paths[0]] = [1, 0] },
            FaceModelIdentity.YuNetSFaceLegacy);

        var migrated = await new PeopleAlbumService(storePath, embeddings).ScanAsync(paths, default);

        migrated.Version.Should().Be(2);
        migrated.ModelIdentity.Should().Be(FaceModelIdentity.YuNetSFaceLegacy.StorageKey);
        migrated.Albums.Should().ContainSingle(album => album.Id == "legacy");
    }

    [Fact]
    public async Task StoreWithDifferentModelIdentity_IsNeverMixed()
    {
        var paths = CreateFiles("arc.jpg");
        var storePath = Path.Combine(_directory, "people.json");
        await File.WriteAllTextAsync(storePath,
            """{"Version":2,"ModelIdentity":"yunet-sface:v1","MatchThreshold":0.62,"Albums":[{"Id":"old","Name":"A","PhotoPaths":[],"MatchCentroids":[[1,0]]}],"RemovedPhotos":{}}""");
        var arcIdentity = new FaceModelIdentity("arcface-r100:test", FaceRecognitionEngineKind.ArcFaceR100, "test", 0.45, 2);
        var embeddings = new FakeEmbeddingService(
            new Dictionary<string, float[]> { [paths[0]] = [1, 0] }, arcIdentity);

        var act = () => new PeopleAlbumService(storePath, embeddings).ScanAsync(paths, default);

        await act.Should().ThrowAsync<FaceModelMismatchException>();
    }

    [Fact]
    public async Task ScanAsync_ReportsProcessedPhotosFacesAndPeople()
    {
        var paths = CreateFiles("first-progress.jpg", "second-progress.jpg");
        var embeddings = new FakeEmbeddingService(new Dictionary<string, float[]>
        {
            [paths[0]] = [1, 0], [paths[1]] = [0, 1]
        });
        var service = new PeopleAlbumService(Path.Combine(_directory, "progress.json"), embeddings);
        var reports = new List<PeopleScanProgress>();

        var result = await service.ScanAsync(paths, new Progress<PeopleScanProgress>(reports.Add), default);

        reports.Should().NotBeEmpty();
        reports[^1].Processed.Should().Be(2);
        reports[^1].Total.Should().Be(2);
        reports[^1].DetectedFaces.Should().Be(2);
        reports[^1].People.Should().Be(2);
        reports[^1].Albums.Should().HaveCount(2);
        reports[^1].Albums.Should().OnlyContain(album =>
            !string.IsNullOrWhiteSpace(album.CoverPath) && album.PhotoCount == 1);
        result.Albums.Should().HaveCount(2);
    }

    [Fact]
    public void RecognitionIdentity_ExposesTheEngineActuallyUsedByScanning()
    {
        var identity = new FaceModelIdentity("arcface-r100:test", FaceRecognitionEngineKind.ArcFaceR100, "licensed-r100", 0.45, 2);
        var service = new PeopleAlbumService(
            Path.Combine(_directory, "identity.json"),
            new FakeEmbeddingService(new Dictionary<string, float[]>(), identity));

        service.ModelIdentity.Should().Be(identity);
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

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
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
