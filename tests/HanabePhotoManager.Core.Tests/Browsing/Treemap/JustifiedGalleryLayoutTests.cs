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

    [Fact]
    public void Arrange_EmitsRowsWithMonotonicallyNonDecreasingY()
    {
        // The viewport-culling optimization binary-searches the justified layout
        // by Y, so rows must be emitted top-to-bottom with non-decreasing Y.
        var layout = new JustifiedGalleryLayout(targetRowHeight: 80, gap: 1);
        IReadOnlyList<(double aspectRatio, string? key)> items = Enumerable.Range(0, 500)
            .Select(index => (aspectRatio: (index % 4) * 0.5 + 0.6, key: (string?)$"p{index}"))
            .ToArray();

        var result = layout.Arrange(items, containerWidth: 1200);

        result.Should().HaveCount(500);
        for (var index = 1; index < result.Count; index++)
        {
            result[index].Y.Should().BeGreaterOrEqualTo(result[index - 1].Y);
        }
    }

    [Fact]
    public void Arrange_IsIndexAlignedWithInput()
    {
        // The renderer maps justifiedItems[i] back to children[i]; the layout
        // must preserve input order so that mapping stays valid.
        var layout = new JustifiedGalleryLayout(targetRowHeight: 80, gap: 1);
        IReadOnlyList<(double aspectRatio, string? key)> items = Enumerable.Range(0, 200)
            .Select(index => (aspectRatio: 0.8 + (index % 3) * 0.4, key: (string?)$"k{index}"))
            .ToArray();

        var result = layout.Arrange(items, containerWidth: 960);

        result.Should().HaveSameCount(items);
        // The layout does not reorder: the Nth result derives from the Nth input.
        for (var index = 0; index < result.Count; index++)
        {
            result[index].AspectRatio.Should().Be(
                Math.Clamp(items[index].aspectRatio, 0.35, 3.5));
        }
    }

    [Fact]
    public void Arrange_CompletesLargeLibrariesWithoutPathologicalCost()
    {
        // 11,739 items mirrors the real all-library wall. Layout must complete
        // in bounded time (well under the generous guard) so republishes that
        // feed aspect-ratio updates never hang the UI thread.
        IReadOnlyList<(double aspectRatio, string? key)> items = Enumerable.Range(0, 11_739)
            .Select(index => (aspectRatio: 0.5 + (index % 7) * 0.35, key: (string?)$"photo-{index}"))
            .ToArray();
        var layout = new JustifiedGalleryLayout(targetRowHeight: 180, gap: 1);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = layout.Arrange(items, containerWidth: 1440);
        stopwatch.Stop();

        result.Should().HaveCount(11_739);
        result[^1].Y.Should().BeGreaterThan(0);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}
