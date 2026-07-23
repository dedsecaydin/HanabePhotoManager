using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using OpenCvSharp;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class OnnxPhotoClassifierTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-onnx-{Guid.NewGuid():N}");

    [Fact]
    public void PreprocessImage_CenterCropsAndNormalizesRgbChannels()
    {
        using var source = new Mat(new Size(320, 240), MatType.CV_8UC3, new Scalar(0, 0, 255));
        using var normalized = OnnxPhotoClassifier.PreprocessImage(source);

        normalized.Size().Should().Be(new Size(224, 224));
        normalized.Type().Should().Be(MatType.CV_32FC3);
        var bgr = normalized.At<Vec3f>(0, 0);
        bgr.Item0.Should().BeApproximately((0f - 0.406f) / 0.225f, 0.001f);
        bgr.Item1.Should().BeApproximately((0f - 0.456f) / 0.224f, 0.001f);
        bgr.Item2.Should().BeApproximately((1f - 0.485f) / 0.229f, 0.001f);
    }

    [Fact]
    public async Task MissingModel_UsesLocalRuleFallbackWithVisibleExplanation()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "photo.jpg");
        using (var image = new Mat(new Size(32, 32), MatType.CV_8UC3, new Scalar(10, 10, 10)))
            Cv2.ImWrite(path, image);
        var fallback = new StubClassifier();
        var classifier = new OnnxPhotoClassifier(
            Path.Combine(_directory, "missing.onnx"),
            Path.Combine(_directory, "missing-labels.txt"),
            fallback);

        var result = await classifier.ClassifyAsync(path, default);

        fallback.CallCount.Should().Be(1);
        result.Labels.Should().ContainSingle().Which.Label.Should().Be("待分类");
        result.Explanation.Should().Contain("ONNX 模型不可用");
    }

    [Fact]
    public async Task ClassifyAsync_CancelledTokenDoesNotRunFallback()
    {
        var fallback = new StubClassifier();
        var classifier = new OnnxPhotoClassifier("missing", "missing", fallback);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => classifier.ClassifyAsync("missing.jpg", cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        fallback.CallCount.Should().Be(0);
    }

    [Fact]
    public void MapImageNetLabels_ProducesSeveralCoarseCategories()
    {
        var mapped = OnnxPhotoClassifier.MapImageNetLabels(
        [
            ("golden retriever", 0.48),
            ("cheeseburger", 0.31),
            ("sports car", 0.14)
        ]);

        mapped.Should().Contain(label => label.Label == "动物");
        mapped.Should().Contain(label => label.Label == "美食");
        mapped.Should().Contain(label => label.Label == "交通");
    }

    [Fact]
    public async Task ShippedModel_LoadsAndRunsInferenceLocally()
    {
        Directory.CreateDirectory(_directory);
        var photoPath = Path.Combine(_directory, "fixture.jpg");
        using (var image = new Mat(new Size(224, 224), MatType.CV_8UC3, new Scalar(80, 150, 210)))
            Cv2.ImWrite(photoPath, image);
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "Models", "Classification");
        using var classifier = new OnnxPhotoClassifier(
            Path.Combine(modelDirectory, "mobilenetv2-7.onnx"),
            Path.Combine(modelDirectory, "imagenet_classes.txt"),
            new StubClassifier(),
            OnnxPhotoClassifier.OfficialModelSha256);

        var result = await classifier.ClassifyAsync(photoPath, default);

        result.EngineId.Should().Be("onnx-mobilenetv2");
        result.Labels.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class StubClassifier : IPhotoClassifier
    {
        public int CallCount { get; private set; }
        public string EngineId => "rules";
        public string Version => "test";

        public Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new PhotoClassificationResult(
                [new PhotoLabelScore("待分类", 1)], EngineId, Version, "测试回退"));
        }
    }
}
