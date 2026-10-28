using System.IO;
using HanabePhotoManager.App.Splitting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImageSplitTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\split-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(2, 1)] [InlineData(1, 2)] [InlineData(3, 2)] [InlineData(2, 3)] [InlineData(3, 3)]
    public void TilesCoverOddSizedCropWithoutOverlap(int columns, int rows)
    {
        var crop = new Rectangle(7, 11, 101, 79);
        var tiles = ImageSplitService.Plan(crop, columns, rows);
        Assert.Equal(columns * rows, tiles.Count);
        Assert.Equal(crop.Width * crop.Height, tiles.Sum(t => t.Width * t.Height));
        var pixels = new HashSet<(int, int)>();
        foreach (var tile in tiles)
            for (int y = tile.Top; y < tile.Bottom; y++)
                for (int x = tile.Left; x < tile.Right; x++) Assert.True(pixels.Add((x, y)));
        Assert.Contains((crop.Right - 1, crop.Bottom - 1), pixels);
    }

    [Fact]
    public void TooSmallOrInvalidGridIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ImageSplitService.Plan(new(0, 0, 2, 2), 3, 3));
        Assert.Throws<ArgumentException>(() => ImageSplitService.Plan(new(0, 0, 20, 20), 0, 2));
    }

    [Fact]
    public async Task ExportUsesExactCropAndNeverOverwritesPriorRun()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.png");
        using (var image = new Image<Rgba32>(17, 13))
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++) image[x, y] = new Rgba32((byte)x, (byte)y, 100);
            await image.SaveAsPngAsync(source);
        }
        var service = new ImageSplitService();
        var first = await service.ExportAsync(source, new(5, 4, 11, 9), 3, 3, _root, true, null, default);
        var second = await service.ExportAsync(source, new(5, 4, 11, 9), 3, 3, _root, true, null, default);
        Assert.NotEqual(first, second);
        Assert.Equal(9, Directory.GetFiles(first).Length);
        using var tile = await Image.LoadAsync<Rgba32>(Path.Combine(first, "01.png"));
        Assert.Equal(new Rgba32(5, 4, 100), tile[0, 0]);
        using var last = await Image.LoadAsync<Rgba32>(Path.Combine(first, "09.png"));
        Assert.Equal(new Rgba32(15, 12, 100), last[last.Width - 1, last.Height - 1]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(source, new(16, 12, 9, 9), 3, 3, _root, true, null, default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExportAsync(source, new(0, 0, 17, 13), 3, 3, _root, true, null, new CancellationToken(true)));
        Assert.Empty(Directory.GetDirectories(_root, ".pending-*"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task MidExportCancellationRemovesPartialFiles()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.png");
        using (var image = new Image<Rgba32>(30, 30)) await image.SaveAsPngAsync(source);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterFirstTile(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ImageSplitService().ExportAsync(
            source, new(0, 0, 30, 30), 3, 3, _root, true, progress, cancellation.Token));
        Assert.Empty(Directory.GetDirectories(_root));
        Assert.True(File.Exists(source));
    }

    [Theory]
    [InlineData(@"C:\temp\split")]
    [InlineData(@"D:\OneDrive\split")]
    public async Task OutputStaysOnDDriveOutsideOneDrive(string output)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new ImageSplitService().ExportAsync(
            "unused.png", new(0, 0, 30, 30), 3, 3, output, true, null, default));
    }

    private sealed class CancelAfterFirstTile(CancellationTokenSource source) : IProgress<double>
    {
        public void Report(double value) => source.Cancel();
    }
}
