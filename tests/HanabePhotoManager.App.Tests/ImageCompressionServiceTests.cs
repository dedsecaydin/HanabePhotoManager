using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using HanabePhotoManager.App.Compression;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageCompressionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-compress-{Guid.NewGuid():N}");

    [Fact]
    public async Task CompressAsync_Jpeg_PreservesDimensionsSourceAndTargetCeiling()
    {
        var source = CreateJpeg("source.jpg", 320, 180, quality: 100);
        var originalHash = SHA256.HashData(File.ReadAllBytes(source));
        var output = Directory.CreateDirectory(Path.Combine(_root, "out")).FullName;
        var target = 18 * 1024L;

        var result = await new ImageCompressionService().CompressAsync(
            new CompressionWorkItem(new CompressionSource(source, new FileInfo(source).Length, 320 * 180), target),
            new CompressionOptions(output), CancellationToken.None);

        result.Status.Should().Be(CompressionItemStatus.Success);
        result.OutputPath.Should().NotBeNull();
        new FileInfo(result.OutputPath!).Length.Should().BeLessThanOrEqualTo(target);
        ReadSize(result.OutputPath!).Should().Be((320, 180));
        SHA256.HashData(File.ReadAllBytes(source)).Should().Equal(originalHash);
    }

    [Fact]
    public async Task CompressAsync_Png_PreservesDimensionsAndTransparency()
    {
        var source = CreateTransparentPng("alpha.png", 64, 48);
        var output = Directory.CreateDirectory(Path.Combine(_root, "out")).FullName;

        var result = await new ImageCompressionService().CompressAsync(
            new CompressionWorkItem(new CompressionSource(source, new FileInfo(source).Length, 64 * 48), 1024 * 1024),
            new CompressionOptions(output), CancellationToken.None);

        result.Status.Should().Be(CompressionItemStatus.Success);
        result.OutputPath.Should().EndWith(".png");
        using (var stream = File.OpenRead(result.OutputPath!))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            decoder.Frames[0].Format.BitsPerPixel.Should().Be(32);
        }
        ReadSize(result.OutputPath!).Should().Be((64, 48));
    }

    [Fact]
    public async Task CompressAsync_UsesUniqueNameInsteadOfOverwriting()
    {
        var source = CreateJpeg("source.jpg", 32, 32, 90);
        var output = Directory.CreateDirectory(Path.Combine(_root, "out")).FullName;
        File.WriteAllText(Path.Combine(output, "source.jpg"), "keep");

        var result = await new ImageCompressionService().CompressAsync(
            new CompressionWorkItem(new CompressionSource(source, new FileInfo(source).Length, 1024), 1024 * 1024),
            new CompressionOptions(output), CancellationToken.None);

        result.OutputPath.Should().EndWith("source (1).jpg");
        File.ReadAllText(Path.Combine(output, "source.jpg")).Should().Be("keep");
    }

    private string CreateJpeg(string name, int width, int height, int quality)
    {
        Directory.CreateDirectory(_root);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var index = (y * width + x) * 4;
            pixels[index] = (byte)(x * 17 + y * 13);
            pixels[index + 1] = (byte)(x * 7 + y * 23);
            pixels[index + 2] = (byte)(x * 29 + y * 3);
            pixels[index + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(_root, name);
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private string CreateTransparentPng(string name, int width, int height)
    {
        Directory.CreateDirectory(_root);
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 220;
            pixels[index + 1] = 80;
            pixels[index + 2] = 20;
            pixels[index + 3] = (byte)((index / 4) % 256);
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(_root, name);
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static (int Width, int Height) ReadSize(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return (decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
