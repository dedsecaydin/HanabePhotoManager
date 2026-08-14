namespace HanabePhotoManager.Core.Browsing.Treemap;

/// <summary>
/// Lays every photo out as a dense, viewport-scaled panorama wall for the
/// lowest semantic zoom band. The logical canvas grows as zoom decreases so
/// the rendered thumbnails retain their minimum recognizable size.
/// </summary>
public sealed class PanoramaPhotoLayout
{
    public const double SemanticZoomThreshold = 0.20;

    private readonly double _gap;
    private readonly double _minimumTileSize;

    public PanoramaPhotoLayout(double minimumTileSize = 32, double gap = 1)
    {
        _minimumTileSize = Math.Max(24, minimumTileSize);
        _gap = Math.Max(0, gap);
    }

    public static bool IsActive(double zoom) =>
        double.IsFinite(zoom) && zoom > 0 && zoom <= SemanticZoomThreshold;

    /// <summary>
    /// Arranges every supplied item in a justified wall. <paramref name="viewportWidth"/>
    /// is measured in screen pixels; the returned bounds are logical control pixels.
    /// </summary>
    public PanoramaLayoutResult Arrange(
        IReadOnlyList<(double aspectRatio, string? key)> items,
        double viewportWidth,
        double zoom)
    {
        if (items.Count == 0 || !double.IsFinite(viewportWidth) || viewportWidth <= 0 || !IsActive(zoom))
        {
            return PanoramaLayoutResult.Empty;
        }

        var logicalWidth = viewportWidth / zoom;
        var logicalMinimumTileSize = _minimumTileSize / zoom;
        var logicalGap = _gap / zoom;
        var gallery = new JustifiedGalleryLayout(
            targetRowHeight: logicalMinimumTileSize,
            minAspect: 0.75,
            maxAspect: 1.8,
            gap: logicalGap,
            minRowFill: 0);
        var arranged = gallery.Arrange(items, logicalWidth);
        var contentHeight = arranged.Count == 0
            ? 0
            : arranged[^1].Y + arranged[^1].Height + logicalGap;

        return new PanoramaLayoutResult(arranged, logicalWidth, contentHeight);
    }
}

public sealed record PanoramaLayoutResult(
    IReadOnlyList<JustifiedItem> Items,
    double ContentWidth,
    double ContentHeight)
{
    public static PanoramaLayoutResult Empty { get; } = new(Array.Empty<JustifiedItem>(), 0, 0);
}
