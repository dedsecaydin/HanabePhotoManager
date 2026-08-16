using FluentAssertions;
using HanabePhotoManager.App.Browsing.Grid;
using Xunit;

namespace HanabePhotoManager.App.Tests.Browsing.Grid;

public sealed class GalleryZoomPolicyTests
{
    [Fact]
    public void MouseWheelWithoutControl_DoesNotRequestZoom()
    {
        GalleryZoomPolicy.ResolveWheelTileSize(150, 120, isControlPressed: false).Should().BeNull();
    }

    [Theory]
    [InlineData(150, 120, 168)]
    [InlineData(150, -120, 133.92857142857142)]
    [InlineData(512, 120, 512)]
    [InlineData(48, -120, 48)]
    public void ControlWheel_UsesOneClampedZoomScale(double current, int delta, double expected)
    {
        GalleryZoomPolicy.ResolveWheelTileSize(current, delta, isControlPressed: true)
            .Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void AnchoredOffset_KeepsThePointedItemInThePointerRowAfterReflow()
    {
        var offset = GalleryZoomPolicy.CalculateAnchoredVerticalOffset(
            oldVerticalOffset: 320,
            pointerX: 410,
            pointerY: 180,
            viewportWidth: 1000,
            oldTileStride: 162,
            newTileStride: 212,
            headerHeight: 64,
            scrollableHeight: 5000);

        offset.Should().BeApproximately(666.5679012, 0.001);
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.NaN)]
    [InlineData(100, -1)]
    public void AnchoredOffset_InvalidLayoutFallsBackToCurrentOffset(double viewportWidth, double newStride)
    {
        GalleryZoomPolicy.CalculateAnchoredVerticalOffset(
            oldVerticalOffset: 240,
            pointerX: 100,
            pointerY: 100,
            viewportWidth,
            oldTileStride: 162,
            newStride,
            headerHeight: 64,
            scrollableHeight: 5000).Should().Be(240);
    }
}
