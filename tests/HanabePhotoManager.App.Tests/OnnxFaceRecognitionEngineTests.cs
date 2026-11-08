using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Tests;

public sealed class OnnxFaceRecognitionEngineTests
{
    [Fact]
    public void WindowsFactoryUsesTheSharedEngine()
    {
        var options = new FaceRecognitionOptions();
        Assert.Same(PortableFaceRecognitionFactory.Create(options), FaceRecognitionEngineFactory.Create(options));
    }

    [Fact]
    public async Task PortableAndExistingEnginesProduceSameClustersForReferenceVariants()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "face-compare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var original = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
            var paths = new[] { Path.Combine(root, "a.jpg"), Path.Combine(root, "b.jpg"), Path.Combine(root, "c.jpg") };
            File.Copy(original, paths[0]);
            using (var image = Image.Load(original)) { image.Mutate(x => x.Brightness(.9f)); image.SaveAsJpeg(paths[1]); }
            using (var image = Image.Load(original)) { image.Mutate(x => x.Brightness(1.1f)); image.SaveAsJpeg(paths[2]); }
            var options = new FaceRecognitionOptions();
            var legacy = new PeopleAlbumService(Path.Combine(root, "old", "albums.json"), LegacyFaceRecognitionEngineFactory.Create(options));
            var portable = new PeopleAlbumService(Path.Combine(root, "new", "albums.json"), PortableFaceRecognitionFactory.Create(options));
            var expected = await legacy.ScanAsync(paths, default);
            var actual = await portable.ScanAsync(paths, default);
            Assert.NotEmpty(expected.Albums);
            Assert.Equal(expected.Albums.Count, actual.Albums.Count);
            Assert.Equal(expected.Albums.Sum(album => album.PhotoPaths.Count), actual.Albums.Sum(album => album.PhotoPaths.Count));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PortableEngineMatchesExistingEngineOnReferenceFace()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var oldEngine = LegacyFaceRecognitionEngineFactory.Create(new FaceRecognitionOptions());
        var portable = PortableFaceRecognitionFactory.Create(new FaceRecognitionOptions());
        var expected = await oldEngine.DetectAsync(source, default);
        var actual = await portable.DetectAsync(source, default);
        Assert.Equal(expected.Count, actual.Count);
        Assert.NotEmpty(actual);
        var similarity = expected[0].Embedding.Zip(actual[0].Embedding, (left, right) => left * right).Sum();
        Assert.True(similarity > .90, $"Embedding similarity: {similarity}");
    }

    [Fact]
    public async Task YuNetSFace_DetectsAlignsAndNormalizesKnownFace()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "face-reference.jpg");
        var engine = FaceRecognitionEngineFactory.Create(new FaceRecognitionOptions());

        var faces = await engine.DetectAsync(source, default);

        faces.Should().NotBeEmpty();
        faces[0].Embedding.Should().NotBeEmpty();
        Math.Sqrt(faces[0].Embedding.Sum(value => value * value)).Should().BeApproximately(1, 0.001);
        faces[0].Width.Should().BePositive();
        faces[0].Height.Should().BePositive();
    }

    [Fact]
    public void Factory_ReusesEngineAndOnnxSessionsForSameConfiguration()
    {
        var options = new FaceRecognitionOptions { Profile = FaceRecognitionProfile.Speed };

        var first = FaceRecognitionEngineFactory.Create(options);
        var second = FaceRecognitionEngineFactory.Create(options);

        second.Should().BeSameAs(first);
    }
}
