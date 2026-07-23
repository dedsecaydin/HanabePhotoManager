using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
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

    private sealed class FakeEmbeddingService(IReadOnlyDictionary<string, float[]> embeddings) : ILocalFaceEmbeddingService
    {
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
