using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using HanabePhotoManager.App.PixelArt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PixelArtRendererTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-pixelart-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(6000, 4000, 256, 256, 171)]
    [InlineData(4000, 6000, 256, 171, 256)]
    [InlineData(8000, 8000, 64, 64, 64)]
    [InlineData(100, 80, 128, 100, 80)]
    public void CalculateGridSize_FitsLongestSideToTarget(int w, int h, int size, int expectedW, int expectedH)
    {
        var (gw, gh) = PixelArtRenderer.CalculateGridSize(w, h, size);
        gw.Should().Be(expectedW);
        gh.Should().Be(expectedH);
    }

    [Theory]
    [InlineData(256, 171, 4)]
    [InlineData(64, 64, 16)]
    [InlineData(128, 128, 8)]
    [InlineData(1, 1, 1024)]
    public void CalculateBlockSize_ScalesLongestSideToAbout1024(int w, int h, int expectedBlock)
    {
        PixelArtRenderer.CalculateBlockSize(w, h).Should().Be(expectedBlock);
    }

    [Fact]
    public void LoadDownscaleExport_Large6000x4000At256_CompletesWithoutCrash()
    {
        RunOnSta(() =>
        {
            Directory.CreateDirectory(_root);
            var source = Path.Combine(_root, "large.png");
            using (var image = new Image<Rgba32>(6000, 4000, Color.CornflowerBlue))
            {
                image.SaveAsPng(source);
            }

            var loaded = PixelArtRenderer.Load(source);
            loaded.Should().NotBeNull();

            var grid = PixelArtRenderer.DownscaleToGrid(loaded, 256, out var width, out var height);
            grid.Should().NotBeNull();
            width.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(256);
            height.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(256);

            var output = Path.Combine(_root, "export.png");
            PixelArtRenderer.Export(grid, output);
            File.Exists(output).Should().BeTrue();

            var block = PixelArtRenderer.CalculateBlockSize(width, height);
            var info = Image.Identify(output);
            info.Width.Should().Be(width * block);
            info.Height.Should().Be(height * block);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw new Xunit.Sdk.XunitException(error.ToString());
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* 忽略清理失败 */ }
    }
}
