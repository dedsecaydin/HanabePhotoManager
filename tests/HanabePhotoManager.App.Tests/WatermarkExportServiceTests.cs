using HanabePhotoManager.App.Watermark;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WatermarkExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-watermark-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Export_PreservesExtensionAndPixelDimensions()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "photo.png");
        var markPath = Path.Combine(_root, "mark.png");
        using (var photo = new Image<Rgba32>(321, 123, Color.CornflowerBlue)) await photo.SaveAsPngAsync(source);
        using (var mark = new Image<Rgba32>(20, 10, new Rgba32(255, 255, 255, 100))) await mark.SaveAsPngAsync(markPath);
        var output = Path.Combine(_root, "out");
        var options = new WatermarkExportOptions(output, "_watermarked", true, WatermarkMode.Signature,
            new(.8, .8, .2, .5), new(true, .5, .2, .2, -20, true, .5));

        var result = Assert.Single(await new WatermarkExportService().ExportAsync([source], markPath, options));

        Assert.Equal(WatermarkExportStatus.Success, result.Status);
        Assert.Equal(".png", Path.GetExtension(result.OutputPath));
        var info = await Image.IdentifyAsync(result.OutputPath!);
        Assert.Equal(321, info.Width);
        Assert.Equal(123, info.Height);
        Assert.True(File.Exists(source));
    }

    public void Dispose() { try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { } }
}
