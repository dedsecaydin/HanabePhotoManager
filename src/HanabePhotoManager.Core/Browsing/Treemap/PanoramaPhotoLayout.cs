namespace HanabePhotoManager.Core.Browsing.Treemap;

/// <summary>
/// 在最低语义缩放区间把全部照片排列为稠密全景墙。缩放越小，逻辑画布越大，
/// 从而让最终显示的缩略图仍保持可辨认的最小尺寸。
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

    /// <summary>判断当前缩放是否进入全景照片墙区间。</summary>
    public static bool IsActive(double zoom) =>
        double.IsFinite(zoom) && zoom > 0 && zoom <= SemanticZoomThreshold;

    /// <summary>
    /// 将所有项目排列为等高填充墙。<paramref name="viewportWidth"/> 使用屏幕像素，
    /// 返回矩形使用控件逻辑坐标。
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

/// <summary>全景照片墙的项目矩形与逻辑画布尺寸。</summary>
public sealed record PanoramaLayoutResult(
    IReadOnlyList<JustifiedItem> Items,
    double ContentWidth,
    double ContentHeight)
{
    public static PanoramaLayoutResult Empty { get; } = new(Array.Empty<JustifiedItem>(), 0, 0);
}
