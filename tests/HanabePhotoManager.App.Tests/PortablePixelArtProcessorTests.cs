using System.IO;
using HanabePhotoManager.App.PixelArt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PortablePixelArtProcessorTests
{
    [Fact]
    public void GridAndExportMaintainAspectAndEnlargePixels()
    {
        using var source = new Image<Rgba32>(32, 16, new Rgba32(255, 0, 0, 255));
        using var input = new MemoryStream();
        source.SaveAsPng(input);
        var grid = PortablePixelArtProcessor.Downscale(input.ToArray(), 16, out var width, out var height);
        Assert.Equal(16, width);
        Assert.Equal(8, height);
        using var export = Image.Load<Rgba32>(PortablePixelArtProcessor.Export(grid));
        Assert.Equal(1024, export.Width);
        Assert.Equal(512, export.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), export[0, 0]);
    }
}
