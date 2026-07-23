using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MobileClipPhotoClassifierTests
{
    [Fact]
    public void RankLabels_ReturnsClosestSemanticLabelsAndDropsDistantOnes()
    {
        var labels = new Dictionary<string, float[]>
        {
            ["人像"] = [1, 0, 0], ["城市"] = [.92f, .08f, 0], ["交通"] = [0, 1, 0]
        };

        var result = MobileClipPhotoClassifier.RankLabels([1, 0, 0], labels);

        result.Select(item => item.Label).Should().ContainInOrder("人像", "城市");
        result.Should().NotContain(item => item.Label == "交通");
    }

    [Fact]
    public async Task ClassifyAsync_ExportedModelRunsInApplicationRuntime()
    {
        var root = FindSourceRoot();
        var modelRoot = Path.Combine(root, "src", "HanabePhotoManager.App", "Models", "MobileCLIP");
        var imagePath = Path.Combine(Path.GetTempPath(), $"mobileclip-{Guid.NewGuid():N}.jpg");
        using (var image = new Image<Rgb24>(320, 240, new Rgb24(45, 110, 180))) await image.SaveAsJpegAsync(imagePath);
        try
        {
            using var classifier = new MobileClipPhotoClassifier(
                Path.Combine(modelRoot, "mobileclip_s2_visual.onnx"),
                Path.Combine(modelRoot, "label_embeddings.json"));
            var result = await classifier.ClassifyAsync(imagePath, default);
            result.EngineVersion.Should().Be(classifier.Version);
            result.Labels.Should().NotBeEmpty();
        }
        finally { File.Delete(imagePath); }
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
