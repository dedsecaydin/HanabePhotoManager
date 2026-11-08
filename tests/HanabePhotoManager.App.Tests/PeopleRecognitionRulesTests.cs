using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>人物识别规则：小脸不建人物、勉强匹配不当参考脸（防止“越吸越多”）、复核把不像的照片移到待确认。</summary>
public sealed class PeopleRecognitionRulesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-people-rules-{Guid.NewGuid():N}");

    public PeopleRecognitionRulesTests() => Directory.CreateDirectory(_directory);

    /// <summary>单位向量：在第 0、1 维之间按角度旋转，cos(两张脸) = cos(角度差)。</summary>
    private static float[] Face(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return [(float)Math.Cos(radians), (float)Math.Sin(radians), 0];
    }

    private string[] Files(params string[] names) => names.Select(name =>
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }).ToArray();

    [Fact]
    public async Task SmallBlurryFaces_DoNotCreatePeople()
    {
        var paths = Files("crowd.jpg");
        var service = new PeopleAlbumService(Path.Combine(_directory, "people.json"),
            new ScriptedFaces(new() { [paths[0]] = [(Face(0), 30, 0.95f), (Face(90), 40, 0.7f)] }));

        var snapshot = await service.ScanAsync(paths, default);

        snapshot.Albums.Should().BeEmpty("远处路人的小脸特征不可靠，不应该各自变成一个“人物”");
    }

    [Fact]
    public async Task BorderlineMatches_JoinButDoNotBecomeReferences_SoTheAlbumCannotDrift()
    {
        // 阈值 0.40。每一步都和上一张勉强像（cos ≈ 0.42），但和第一张越来越不像。
        // 旧规则：每张勉强匹配的脸都成为参考脸 → 一路串起完全不同的人。
        var paths = Files("a.jpg", "b.jpg", "c.jpg", "d.jpg");
        var service = new PeopleAlbumService(Path.Combine(_directory, "people.json"),
            new ScriptedFaces(new()
            {
                [paths[0]] = [(Face(0), 200, 0.95f)],
                [paths[1]] = [(Face(65), 200, 0.95f)],
                [paths[2]] = [(Face(130), 200, 0.95f)],
                [paths[3]] = [(Face(195), 200, 0.95f)],
            }));

        var snapshot = await service.ScanAsync(paths, default);

        var first = snapshot.Albums.Single(album => album.PhotoPaths.Contains(paths[0]));
        first.PhotoPaths.Should().BeEquivalentTo([paths[0], paths[1]], "只有和参考脸本身像的照片才归进来");
        first.MatchCentroids.Should().HaveCount(1, "勉强匹配的脸不能成为新的参考脸");
    }

    [Fact]
    public async Task Review_MovesPhotosThatDoNotMatchTheCoreLook_AndCanBeUndone()
    {
        var paths = Files("p1.jpg", "p2.jpg", "p3.jpg", "stranger.jpg", "noface.jpg");
        var faces = new Dictionary<string, (float[] Embedding, int Size, float Confidence)[]>
        {
            [paths[0]] = [(Face(0), 200, 0.95f)],
            [paths[1]] = [(Face(10), 200, 0.95f)],
            [paths[2]] = [(Face(20), 200, 0.95f)],
            [paths[3]] = [(Face(64), 200, 0.95f)],
            [paths[4]] = [],
        };
        var store = Path.Combine(_directory, "people.json");
        // 模拟旧数据：一个人物已经被“吸”进了陌生人和没有脸的照片，参考脸里混进了一张偏的。
        var seed = new PeopleAlbumService(store, new ScriptedFaces(faces));
        await seed.ScanAsync(paths.Take(3), default);
        var snapshotJson = await File.ReadAllTextAsync(store);
        var polluted = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(snapshotJson)!;
        var album = polluted["Albums"]![0]!.AsObject();
        album["PhotoPaths"]!.AsArray().Add(paths[3]);
        album["PhotoPaths"]!.AsArray().Add(paths[4]);
        album["MatchCentroids"]!.AsArray().Add(System.Text.Json.JsonSerializer.SerializeToNode(Face(150)));
        await File.WriteAllTextAsync(store, polluted.ToJsonString());
        var albumId = album["Id"]!.GetValue<string>();

        var service = new PeopleAlbumService(store, new ScriptedFaces(faces));
        var result = await service.ReviewAlbumAsync(albumId, null, default);

        result.Moved.Should().Be(2);
        result.Kept.Should().Be(3);
        var reviewed = await service.LoadAsync();
        reviewed.Albums.Single(a => a.Id == albumId).PhotoPaths.Should().BeEquivalentTo(paths.Take(3));
        reviewed.Albums.Single(a => a.Id == result.ReviewAlbumId).PhotoPaths.Should().BeEquivalentTo([paths[3], paths[4]]);
        reviewed.Albums.Single(a => a.Id == albumId).MatchCentroids.Should().NotContain(c => Math.Abs(c[0] - Face(150)[0]) < 1e-4);

        (await service.UndoAsync()).Should().BeTrue();
        (await service.LoadAsync()).Albums.Single(a => a.Id == albumId).PhotoPaths.Should().HaveCount(5);
    }

    [Fact]
    public void CoreCentroids_DropTheReferenceThatDriftedAway()
    {
        var core = PeopleAlbumService.CoreCentroids([Face(0), Face(8), Face(15), Face(120)]);
        core.Should().HaveCount(3);
        core.Should().NotContain(c => Math.Abs(c[1] - Face(120)[1]) < 1e-4);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch { }
    }

    private sealed class ScriptedFaces(Dictionary<string, (float[] Embedding, int Size, float Confidence)[]> faces) : ILocalFaceEmbeddingService
    {
        public FaceModelIdentity ModelIdentity { get; } = FaceModelIdentity.YuNetSFaceLegacy with { MatchThreshold = 0.40 };

        public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken)
        {
            IReadOnlyList<DetectedFace> result = faces.TryGetValue(path, out var list)
                ? list.Select(face => new DetectedFace(path, face.Embedding, 0, 0, face.Size, face.Size, face.Confidence)).ToArray()
                : [];
            return Task.FromResult(result);
        }
    }
}
