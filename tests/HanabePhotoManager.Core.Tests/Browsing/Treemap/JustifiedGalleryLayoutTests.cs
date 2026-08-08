using FluentAssertions;
using HanabePhotoManager.Core.Browsing.Treemap;

namespace HanabePhotoManager.Core.Tests.Browsing.Treemap;

public sealed class JustifiedGalleryLayoutTests
{
    [Fact]
    public void Arrange_FillsCompletedRowUsingImageAspectRatios()
    {
        var layout = new JustifiedGalleryLayout(targetRowHeight: 100, gap: 2);

        var result = layout.Arrange(
            [(1d, "portrait"), (2d, "landscape"), (1d, "square")],
            containerWidth: 400);

        result.Should().HaveCount(3);
        result.Select(item => item.Height).Should().OnlyContain(height => height == result[0].Height);
        result.Sum(item => item.Width).Should().BeApproximately(396, 0.001);
        result[1].Width.Should().BeApproximately(result[0].Width * 2, 0.001);
    }

    [Fact]
    public void Arrange_KeepsSparseFinalRowAtTargetHeight()
    {
        var layout = new JustifiedGalleryLayout(targetRowHeight: 100, gap: 2, minRowFill: 0.7);

        var result = layout.Arrange([(1d, "only")], containerWidth: 400);

        result.Should().ContainSingle();
        result[0].Height.Should().Be(100);
        result[0].Width.Should().Be(100);
    }
}
