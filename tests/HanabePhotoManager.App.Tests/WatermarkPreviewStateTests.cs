using FluentAssertions;
using HanabePhotoManager.App.Watermark;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WatermarkPreviewStateTests
{
    [Fact]
    public void TilePreviewViewport_ChangesWhenLiveParametersChange()
    {
        var viewModel = new WatermarkViewModel();
        var initial = viewModel.PreviewTileViewport;

        viewModel.Density = 0.9;
        var denser = viewModel.PreviewTileViewport;
        viewModel.IsManualTile = true;
        viewModel.HorizontalGap = 0.6;
        var widerGap = viewModel.PreviewTileViewport;

        denser.Should().NotBe(initial);
        widerGap.Should().NotBe(denser);
    }

    [Fact]
    public void TilePreviewAngle_FollowsManualRotation()
    {
        var viewModel = new WatermarkViewModel { IsManualTile = true };

        viewModel.Angle = 37;

        viewModel.PreviewTileAngle.Should().Be(37);
    }

    [Fact]
    public void WatermarkSource_IsRemovedFromThePhotoQueue()
    {
        var directory = Directory.CreateTempSubdirectory("hanabe-watermark-test-");
        try
        {
            var photo = Path.Combine(directory.FullName, "photo.jpg");
            var watermark = Path.Combine(directory.FullName, "signature.png");
            File.WriteAllBytes(photo, [1]);
            File.WriteAllBytes(watermark, [1]);
            var viewModel = new WatermarkViewModel();
            viewModel.AddInputs([photo, watermark]);

            viewModel.SetWatermark(watermark);

            viewModel.Items.Select(item => item.Path).Should().Contain(photo).And.NotContain(watermark);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
