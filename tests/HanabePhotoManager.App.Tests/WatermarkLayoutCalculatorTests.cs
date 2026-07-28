using HanabePhotoManager.App.Watermark;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WatermarkLayoutCalculatorTests
{
    [Fact]
    public void Single_UsesNormalizedCenterAndProportionalWidth()
    {
        var placement = WatermarkLayoutCalculator.CalculateSingle(2000, 1000, 400, 100,
            new WatermarkLayoutSettings(0.75, 0.8, 0.2, 0.65));
        Assert.Equal(400, placement.Width);
        Assert.Equal(100, placement.Height);
        Assert.Equal(1300, placement.X);
        Assert.Equal(750, placement.Y);
        Assert.Equal(0.65, placement.Opacity);
    }

    [Fact]
    public void Single_ClampsInsideImage()
    {
        var placement = WatermarkLayoutCalculator.CalculateSingle(100, 100, 50, 25,
            new WatermarkLayoutSettings(1, 0, 0.8, 1));
        Assert.InRange(placement.X, 0, 20);
        Assert.Equal(0, placement.Y);
        Assert.Equal(80, placement.Width);
    }

    [Fact]
    public void Tiled_AutomaticDensityProducesMoreTilesAtHigherDensity()
    {
        var sparse = WatermarkLayoutCalculator.CalculateTiled(1200, 800, 300, 100,
            new WatermarkTileSettings(true, 0.1, 0.2, 0.2, -25, true, 0.5));
        var dense = WatermarkLayoutCalculator.CalculateTiled(1200, 800, 300, 100,
            new WatermarkTileSettings(true, 0.9, 0.2, 0.2, -25, true, 0.5));
        Assert.True(dense.Count > sparse.Count);
        Assert.All(dense, item => Assert.Equal(0.5, item.Opacity));
    }

    [Fact]
    public void Tiled_ManualHonorsSpacingRotationAndStagger()
    {
        var items = WatermarkLayoutCalculator.CalculateTiled(1000, 600, 200, 100,
            new WatermarkTileSettings(false, 0.5, 0.25, 0.3, -18, true, 0.7));
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(-18, item.RotationDegrees));
        Assert.True(items.Select(item => item.X).Distinct().Count() > 2);
    }

    [Fact]
    public void WatermarkPage_ExposesDirectDragInteractionsAndExportAction()
    {
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "Watermark", "WatermarkPage.xaml"));

        Assert.Contains("MouseMove=\"Preview_MouseMove\"", xaml);
        Assert.Contains("MouseLeftButtonUp=\"Preview_MouseLeftButtonUp\"", xaml);
        Assert.Contains("Drop=\"Watermark_Drop\"", xaml);
        Assert.Contains("Content=\"导出水印图片\"", xaml);
        Assert.Contains("Content=\"文件夹批处理\"", xaml);
        Assert.Contains("Command=\"{Binding StartFolderBatchCommand}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding FolderSources}\"", xaml);
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
