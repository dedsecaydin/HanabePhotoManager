using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class FaceSearchServiceTests
{
    [Fact]
    public void PortableSearchUsesLargestFaceAsReferenceCandidate()
    {
        using var engine = new PortableFaceSearchEngine(new TwoFaceEngine());
        var embeddings = engine.ExtractEmbeddings("unused", default);
        Assert.Equal(2f, embeddings[0][0]);
    }

    private sealed class TwoFaceEngine : ILocalFaceEmbeddingService
    {
        public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<DetectedFace>>([new(path, [1], 0, 0, 30, 30), new(path, [2], 0, 0, 80, 80)]);
    }

    [Fact]
    public async Task Reference_and_identical_library_photo_produce_a_match()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var library = Path.Combine(@"D:\HanabePhoto\.artifacts", $"hanabe-face-{Guid.NewGuid():N}");
        Directory.CreateDirectory(library);
        var libraryPhoto = Path.Combine(library, "JK0001.jpg");
        File.Copy(source, libraryPhoto);

        try
        {
            var service = new FaceSearchService(() => WindowsFaceSearchEngine.Create(), Path.Combine(library, ".cache", "legacy.json"));
            var reference = await service.CreateReferenceAsync(source, CancellationToken.None);
            var matches = await service.SearchAsync(
                reference, library, 0.42, progress: null, CancellationToken.None);

            reference.Embedding.Should().NotBeEmpty();
            matches.Should().ContainSingle();
            matches[0].Path.Should().Be(libraryPhoto);
            matches[0].Similarity.Should().BeGreaterThan(0.95);
        }
        finally
        {
            Directory.Delete(library, recursive: true);
        }
    }

    [Fact]
    public async Task PortableSearchFindsIdenticalFaceInIsolatedLibrary()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "face-search-portable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var photo = Path.Combine(root, "photo.jpg");
            File.Copy(source, photo);
            var service = new FaceSearchService(() => new PortableFaceSearchEngine(PortableFaceRecognitionFactory.Create(new FaceRecognitionOptions())), Path.Combine(root, ".cache", "portable.json"));
            var reference = await service.CreateReferenceAsync(source, default);
            var matches = await service.SearchAsync(reference, root, .42, null, default);
            Assert.Contains(matches, item => item.Path == photo && item.Similarity > .95);
        }
        finally { Directory.Delete(root, true); }
    }
}
