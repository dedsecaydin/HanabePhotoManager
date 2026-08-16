using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.App.Watermark;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>图片工具压力测试：构造大量测试图，验证批量处理不崩溃并测得上限。</summary>
public sealed class ImageToolStressTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-stress-{Guid.NewGuid():N}");

    [Fact]
    public async Task Compression_400Images_CompletesWithoutCrash()
    {
        var sourceDir = EnsureDir("src");
        var paths = GenerateImages(sourceDir, 400, 200, 150);

        var sources = paths.Select(p => new CompressionSource(p, new FileInfo(p).Length, 200 * 150)).ToArray();
        var plan = new ImageCompressionPlanner().CreatePlan(sources, CompressionTargetMode.PerImage, 30 * 1024);
        plan.Should().HaveCount(400);

        var output = EnsureDir("compressed");
        var service = new ImageCompressionService();
        var results = new System.Collections.Generic.List<CompressionItemResult>();
        foreach (var item in plan)
        {
            results.Add(await service.CompressAsync(item, new CompressionOptions(output), CancellationToken.None));
        }

        results.Should().HaveCount(400);
        results.Count(r => r.Status == CompressionItemStatus.Success).Should().BeGreaterThan(300);
    }

    [Fact]
    public async Task Collage_200Images_CompletesWithoutCrash()
    {
        var sourceDir = EnsureDir("collage-src");
        var paths = GenerateImages(sourceDir, 200, 100, 100);

        var result = await new ImageCollageService().ComposeAsync(
            paths,
            new CollageOptions(EnsureDir("collage-out"), CollageOrientation.Vertical, TargetBytes: null),
            progress: null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Width.Should().Be(100);
        result.Height.Should().Be(200 * 100);
        File.Exists(result.OutputPath).Should().BeTrue();
    }

    [Fact]
    public async Task Watermark_500Images_CompletesWithoutCrash()
    {
        var sourceDir = EnsureDir("wm-src");
        var paths = GenerateImages(sourceDir, 500, 200, 200);

        var markPath = Path.Combine(_root, "mark.png");
        using (var mark = new Image<Rgba32>(20, 10, new Rgba32(255, 255, 255, 100)))
        {
            mark.SaveAsPng(markPath);
        }

        var options = new WatermarkExportOptions(
            EnsureDir("wm-out"), "_wm", true, WatermarkMode.Signature,
            new(0.8, 0.8, 0.2, 0.5), new(true, 0.5, 0.2, 0.2, -20, true, 0.5), MaxParallelism: 4);

        var results = await new WatermarkExportService().ExportAsync(paths, markPath, options);

        results.Should().HaveCount(500);
        results.Count(r => r.Status == WatermarkExportStatus.Success).Should().Be(500);
    }

    private string EnsureDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static System.Collections.Generic.List<string> GenerateImages(string dir, int count, int width, int height)
    {
        var paths = new System.Collections.Generic.List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(dir, $"img{i:000}.jpg");
            using var image = new Image<Rgba32>(width, height, Color.FromRgb((byte)((i * 37) % 255), (byte)((i * 91) % 255), (byte)((i * 53) % 255)));
            image.SaveAsJpeg(path);
            paths.Add(path);
        }
        return paths;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* 忽略清理失败 */ }
    }
}
