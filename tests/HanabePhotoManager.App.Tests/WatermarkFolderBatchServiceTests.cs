using HanabePhotoManager.App.Watermark;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WatermarkFolderBatchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-watermark-folders-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsync_FindsSupportedImagesAndPreservesRelativeDirectories()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(source, "album", "day")).FullName;
        File.WriteAllText(Path.Combine(source, "notes.txt"), "ignored");
        await CreateImageAsync(Path.Combine(source, "cover.jpg"));
        await CreateImageAsync(Path.Combine(child, "photo.png"));

        var result = await new WatermarkFolderBatchService().ScanAsync([source], Path.Combine(_root, "output"), recursive: true);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.SourcePath == Path.Combine(source, "cover.jpg") && item.RelativeDirectory == "");
        Assert.Contains(result.Items, item => item.SourcePath == Path.Combine(child, "photo.png") && item.RelativeDirectory == Path.Combine("album", "day"));
    }

    [Fact]
    public async Task ScanAsync_ExcludesHiddenItemsAndOutputTree()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var hiddenDirectory = Directory.CreateDirectory(Path.Combine(source, "hidden")).FullName;
        File.SetAttributes(hiddenDirectory, File.GetAttributes(hiddenDirectory) | FileAttributes.Hidden);
        await CreateImageAsync(Path.Combine(hiddenDirectory, "hidden.png"));
        var hiddenFile = Path.Combine(source, "secret.png");
        await CreateImageAsync(hiddenFile);
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
        var output = Directory.CreateDirectory(Path.Combine(source, "output")).FullName;
        await CreateImageAsync(Path.Combine(output, "old.png"));
        await CreateImageAsync(Path.Combine(source, "visible.png"));

        var result = await new WatermarkFolderBatchService().ScanAsync([source], output, recursive: true);

        var item = Assert.Single(result.Items);
        Assert.Equal(Path.Combine(source, "visible.png"), item.SourcePath);
    }

    [Fact]
    public async Task ScanAsync_WhenNotRecursive_OnlyFindsRootImages()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(source, "child")).FullName;
        await CreateImageAsync(Path.Combine(source, "root.png"));
        await CreateImageAsync(Path.Combine(child, "nested.png"));

        var result = await new WatermarkFolderBatchService().ScanAsync([source], Path.Combine(_root, "output"), recursive: false);

        Assert.Single(result.Items);
        Assert.Equal(Path.Combine(source, "root.png"), result.Items[0].SourcePath);
    }

    [Fact]
    public async Task ProcessAsync_PreservesStructureAndAppendsSequenceForExistingName()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(source, "album")).FullName;
        await CreateImageAsync(Path.Combine(child, "photo.png"));
        var watermark = Path.Combine(_root, "mark.png");
        await CreateImageAsync(watermark, transparent: true);
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        var expectedDirectory = Directory.CreateDirectory(Path.Combine(output, "album")).FullName;
        await CreateImageAsync(Path.Combine(expectedDirectory, "photo_watermarked.png"));
        var service = new WatermarkFolderBatchService();
        var scan = await service.ScanAsync([source], output, recursive: true);
        var options = new WatermarkExportOptions(output, "_watermarked", true, WatermarkMode.Signature,
            new(.8, .8, .2, .5), new(true, .5, .2, .2, -20, true, .5));

        var result = await service.ProcessAsync(scan.Items, watermark, options);

        Assert.Equal(1, result.Success);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(Path.Combine(expectedDirectory, "photo_watermarked (1).png")));
        Assert.True(File.Exists(Path.Combine(child, "photo.png")));
    }

    [Fact]
    public async Task ScanAsync_HonorsCancellation()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new WatermarkFolderBatchService().ScanAsync([source], Path.Combine(_root, "output"), true, cancellation.Token));
    }

    private static async Task CreateImageAsync(string path, bool transparent = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgba32>(24, 16, transparent ? new Rgba32(255, 255, 255, 100) : Color.CornflowerBlue);
        if (Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase)) await image.SaveAsJpegAsync(path);
        else await image.SaveAsPngAsync(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
        }
    }
}
