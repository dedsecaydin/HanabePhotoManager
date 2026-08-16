namespace HanabePhotoManager.App.Browsing.Grid;

/// <summary>
/// Pure calculations for the photo-library grid zoom interaction.
/// WPF event handling stays in the view; all zoom entry points share these rules.
/// </summary>
public static class GalleryZoomPolicy
{
    public const double MinimumTileSize = 48;
    public const double MaximumTileSize = 512;
    public const double DefaultTileSize = 150;
    public const double TileSpacing = 12;
    public const double HeaderHeight = 64;
    public const double WheelNotchFactor = 1.12;

    public static double? ResolveWheelTileSize(double currentTileSize, int wheelDelta, bool isControlPressed)
    {
        if (!isControlPressed || wheelDelta == 0 || !double.IsFinite(currentTileSize))
        {
            return null;
        }

        var factor = wheelDelta > 0 ? WheelNotchFactor : 1 / WheelNotchFactor;
        return Math.Clamp(currentTileSize * factor, MinimumTileSize, MaximumTileSize);
    }

    public static double CalculateAnchoredVerticalOffset(
        double oldVerticalOffset,
        double pointerX,
        double pointerY,
        double viewportWidth,
        double oldTileStride,
        double newTileStride,
        double headerHeight,
        double scrollableHeight)
    {
        if (!AreFinite(oldVerticalOffset, pointerX, pointerY, viewportWidth, oldTileStride, newTileStride, headerHeight, scrollableHeight) ||
            viewportWidth <= 0 || oldTileStride <= 0 || newTileStride <= 0)
        {
            return oldVerticalOffset;
        }

        var oldColumns = Math.Max(1, (int)Math.Floor(viewportWidth / oldTileStride));
        var newColumns = Math.Max(1, (int)Math.Floor(viewportWidth / newTileStride));
        var contentY = Math.Max(0, oldVerticalOffset + pointerY - headerHeight);
        var oldRow = Math.Max(0, (int)Math.Floor(contentY / oldTileStride));
        var oldColumn = Math.Clamp((int)Math.Floor(Math.Max(0, pointerX) / oldTileStride), 0, oldColumns - 1);
        var itemIndex = oldRow * oldColumns + oldColumn;
        var rowProgress = (contentY - oldRow * oldTileStride) / oldTileStride;
        var newRow = itemIndex / newColumns;
        var anchoredContentY = newRow * newTileStride + rowProgress * newTileStride;
        var requestedOffset = headerHeight + anchoredContentY - pointerY;

        return Math.Clamp(requestedOffset, 0, Math.Max(0, scrollableHeight));
    }

    private static bool AreFinite(params double[] values) => values.All(double.IsFinite);
}
