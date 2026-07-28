using HanabePhotoManager.App.Watermark;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WatermarkFolderBatchViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-watermark-folder-vm-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FolderSources_AreIndependentFromExistingImageQueue()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var viewModel = new WatermarkViewModel();

        viewModel.AddSourceFolders([source]);

        Assert.Single(viewModel.FolderSources);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasItems);
    }

    [Fact]
    public async Task ScanFoldersCommand_UpdatesScanCountWithoutPopulatingImageQueue()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        await CreateImageAsync(Path.Combine(source, "photo.png"));
        var viewModel = new WatermarkViewModel
        {
            FolderOutputDirectory = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName,
        };
        viewModel.AddSourceFolders([source]);

        await viewModel.ScanFoldersCommand.ExecuteAsync(null);

        Assert.Equal(1, viewModel.FolderScanCount);
        Assert.Empty(viewModel.Items);
        Assert.Contains("1", viewModel.FolderStatusText);
    }

    [Fact]
    public async Task StartFolderBatchCommand_ReusesWatermarkSettingsAndUpdatesCounts()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        await CreateImageAsync(Path.Combine(source, "photo.png"));
        var watermark = Path.Combine(_root, "mark.png");
        await CreateImageAsync(watermark, transparent: true);
        var output = Directory.CreateDirectory(Path.Combine(_root, "output")).FullName;
        var viewModel = new WatermarkViewModel
        {
            FolderOutputDirectory = output,
            WatermarkPath = watermark,
        };
        viewModel.AddSourceFolders([source]);
        await viewModel.ScanFoldersCommand.ExecuteAsync(null);

        await viewModel.StartFolderBatchCommand.ExecuteAsync(null);

        Assert.Equal(1, viewModel.FolderSuccessCount);
        Assert.Equal(0, viewModel.FolderFailedCount);
        Assert.True(File.Exists(Path.Combine(output, "photo_watermarked.png")));
        Assert.Empty(viewModel.Items);
    }

    private static async Task CreateImageAsync(string path, bool transparent = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgba32>(24, 16, transparent ? new Rgba32(255, 255, 255, 100) : Color.CornflowerBlue);
        await image.SaveAsPngAsync(path);
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
