using System.IO;
using HanabePhotoManager.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortableRuleBasedPhotoClassifierTests
{
    [Theory]
    [InlineData(10, 10, 10)]
    [InlineData(20, 190, 30)]
    public async Task SolidSamplesMatchWindowsRuleCategories(byte red, byte green, byte blue)
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "rules-compare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "sample.png");
            using (var image = new Image<Rgb24>(100, 80, new Rgb24(red, green, blue))) image.SaveAsPng(path);
            var legacy = await new RuleBasedPhotoClassifier(_ => false).ClassifyAsync(path, default);
            var portable = await new PortableRuleBasedPhotoClassifier((_, _) => Task.FromResult(false)).ClassifyAsync(path, default);
            Assert.Equal(legacy.Labels.Select(item => item.Label), portable.Labels.Select(item => item.Label));
        }
        finally { Directory.Delete(root, true); }
    }
}
