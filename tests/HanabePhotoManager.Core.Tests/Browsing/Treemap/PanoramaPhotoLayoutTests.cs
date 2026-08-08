using FluentAssertions;
using HanabePhotoManager.Core.Browsing.Treemap;

namespace HanabePhotoManager.Core.Tests.Browsing.Treemap;

public sealed class PanoramaPhotoLayoutTests
{
    [Fact]
    public void Arrange_IncludesEveryPhotoAndKeepsViewportTileHeightAtMinimum()
    {
        var layout = new PanoramaPhotoLayout(minimumTileSize: 32, gap: 1);

        var result = layout.Arrange(
            [(0.5d, "portrait"), (1d, "square"), (1.8d, "wide"), (1d, "four")],
            viewportWidth: 960,
            zoom: 0.1);

        result.Items.Should().HaveCount(4);
        result.ContentWidth.Should().BeApproximately(9600, 0.001);
        result.Items.Should().OnlyContain(item => item.Height * 0.1 >= 32);
    }

    [Fact]
    public void Arrange_UsesAllItemsInsteadOfAnOverviewSample()
    {
        var layout = new PanoramaPhotoLayout();
        IReadOnlyList<(double aspectRatio, string? key)> photos = Enumerable.Range(0, 6_217)
            .Select(index => (aspectRatio: (index % 3) + 0.75d, key: (string?)$"photo-{index}"))
            .ToArray();

        var result = layout.Arrange(photos, viewportWidth: 1200, zoom: 0.02);

        result.Items.Should().HaveCount(6_217);
        result.ContentHeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0.20, true)]
    [InlineData(0.21, false)]
    public void IsActive_UsesTheMinimumSemanticZoomBand(double zoom, bool expected)
    {
        PanoramaPhotoLayout.IsActive(zoom).Should().Be(expected);
    }
}
