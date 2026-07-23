using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using OpenCvSharp;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RuleBasedPhotoClassifierTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-rules-{Guid.NewGuid():N}");

    [Fact]
    public async Task ClassifyAsync_DarkImageProducesNightLabel()
    {
        var path = WriteSolid("dark.jpg", new Scalar(12, 14, 18));
        var result = await new RuleBasedPhotoClassifier(_ => false).ClassifyAsync(path, default);

        result.Labels.Should().Contain(label => label.Label == "夜景" && label.Score >= 0.7);
        result.EngineId.Should().Be("rules");
    }

    [Fact]
    public async Task ClassifyAsync_GreenDominantImageProducesNatureLabels()
    {
        var path = WriteSolid("green.jpg", new Scalar(20, 180, 40));
        var result = await new RuleBasedPhotoClassifier(_ => false).ClassifyAsync(path, default);

        result.Labels.Should().Contain(label => label.Label == "自然风景");
        result.Labels.Should().Contain(label => label.Label == "植物");
    }

    [Fact]
    public async Task ClassifyAsync_DetectedFaceProducesPortraitAsTopLabel()
    {
        var path = WriteSolid("face.jpg", new Scalar(120, 150, 180));
        var result = await new RuleBasedPhotoClassifier(_ => true).ClassifyAsync(path, default);

        result.Labels.First().Label.Should().Be("人像");
        result.Explanation.Should().Contain("人脸");
    }

    [Fact]
    public async Task ClassifyAsync_NeutralLowSignalImageFallsBackToUnclassified()
    {
        var path = WriteSolid("neutral.jpg", new Scalar(128, 128, 128));
        var result = await new RuleBasedPhotoClassifier(_ => false).ClassifyAsync(path, default);

        result.Labels.Should().ContainSingle().Which.Label.Should().Be("待分类");
    }

    [Fact]
    public async Task ClassifyAsync_AlreadyCancelledStopsBeforeDecode()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => new RuleBasedPhotoClassifier(_ => false)
            .ClassifyAsync(Path.Combine(_directory, "missing.jpg"), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private string WriteSolid(string name, Scalar color)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        using var image = new Mat(new Size(180, 120), MatType.CV_8UC3, color);
        Cv2.ImWrite(path, image);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
