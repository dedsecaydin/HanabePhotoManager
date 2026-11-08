namespace HanabePhotoManager.App.Services;

public sealed record VisibleMapTile(int X, int Y, double Left, double Top);

/// <summary>Calculates only tiles intersecting the current visible map rectangle.</summary>
public static class MapViewport
{
    public const int TileSize = 256;

    public static IReadOnlyList<VisibleMapTile> VisibleTiles(double latitude, double longitude, int zoom, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || width > 8192 || height > 8192)
            throw new ArgumentOutOfRangeException(nameof(width));
        var center = MapTileProjection.Project(latitude, longitude, zoom);
        var left = center.X * TileSize - width / 2;
        var top = center.Y * TileSize - height / 2;
        var firstX = (int)Math.Floor(left / TileSize);
        var lastX = (int)Math.Ceiling((left + width) / TileSize) - 1;
        var firstY = (int)Math.Floor(top / TileSize);
        var lastY = (int)Math.Ceiling((top + height) / TileSize) - 1;
        var count = 1 << zoom;
        var result = new List<VisibleMapTile>();
        for (var y = Math.Max(firstY, 0); y <= Math.Min(lastY, count - 1); y++)
        for (var x = firstX; x <= lastX; x++)
        {
            var wrappedX = ((x % count) + count) % count;
            result.Add(new VisibleMapTile(wrappedX, y, x * (double)TileSize - left, y * (double)TileSize - top));
        }
        return result;
    }
}
