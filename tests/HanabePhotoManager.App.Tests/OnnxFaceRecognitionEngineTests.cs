using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;
using System.IO;

namespace HanabePhotoManager.App.Tests;

public sealed class OnnxFaceRecognitionEngineTests
{
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
