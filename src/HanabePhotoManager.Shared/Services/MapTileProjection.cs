namespace HanabePhotoManager.App.Services;

/// <summary>Web Mercator coordinates expressed in slippy-map tile units.</summary>
public static class MapTileProjection
{
    private const double MaximumLatitude = 85.0511287798066;

    public static (double X, double Y) Project(double latitude, double longitude, int zoom)
    {
        ValidateZoom(zoom);
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)) throw new ArgumentOutOfRangeException(nameof(latitude));
        latitude = Math.Clamp(latitude, -MaximumLatitude, MaximumLatitude);
        longitude = ((longitude + 180) % 360 + 360) % 360 - 180;
        var count = 1 << zoom;
        var radians = latitude * Math.PI / 180;
        var x = (longitude + 180) / 360 * count;
        var y = (1 - Math.Asinh(Math.Tan(radians)) / Math.PI) / 2 * count;
        return (x, Math.Clamp(y, 0, count));
    }

    public static (double Latitude, double Longitude) Unproject(double x, double y, int zoom)
    {
        ValidateZoom(zoom);
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        var count = 1 << zoom;
        x = ((x % count) + count) % count;
        y = Math.Clamp(y, 0, count);
        return (Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / count))) * 180 / Math.PI, x / count * 360 - 180);
    }

    private static void ValidateZoom(int zoom)
    {
        if (zoom is < 0 or > 22) throw new ArgumentOutOfRangeException(nameof(zoom));
    }
}
