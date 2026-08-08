using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class SigLip2PhotoClassifierTests
{
    [Fact]
    public void RankLabels_UsesL2NormalizedCosineSimilarityWithoutProbabilityRemapping()
    {
        var labels = new Dictionary<string, float[]>
        {
            ["portrait"] = [10, 0],
            ["city"] = [.8f, .6f]
        };

        var ranked = SigLip2PhotoClassifier.RankLabels([3, 0], labels, 2, 2);

        ranked[0].Label.Should().Be("portrait");
        ranked[0].Score.Should().Be(1);
        ranked[1].Score.Should().Be(.8);
    }

    [Fact]
    public void Factory_ExposesBothSemanticModels()
    {
        PhotoClassifierFactory.SemanticModes.Should().Contain(PhotoClassifierFactory.MobileClipMode);
        PhotoClassifierFactory.SemanticModes.Should().Contain(PhotoClassifierFactory.SigLip2Mode);
    }

    [Fact]
    public async Task ClassifyAsync_ExportedModelRunsInApplicationRuntime()
    {
        var root = FindSourceRoot();
        var modelRoot = Path.Combine(root, "src", "HanabePhotoManager.App", "Models", "SigLIP2");
        var imagePath = Path.Combine(Path.GetTempPath(), $"siglip2-{Guid.NewGuid():N}.png");
        using (var image = new Image<Rgb24>(300, 220, new Rgb24(38, 112, 186)))
            await image.SaveAsPngAsync(imagePath);
        try
        {
            using var classifier = new SigLip2PhotoClassifier(
                Path.Combine(modelRoot, "siglip2_visual.onnx"),
                Path.Combine(modelRoot, "label_embeddings.json"));
            var result = await classifier.ClassifyAsync(imagePath, default);
            result.EngineId.Should().Be(classifier.EngineId);
            result.Labels.Should().NotBeEmpty();
            result.Labels.Should().OnlyContain(item => item.Score >= -1 && item.Score <= 1);
        }
        finally { File.Delete(imagePath); }
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
