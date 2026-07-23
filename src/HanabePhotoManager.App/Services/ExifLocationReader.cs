using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace HanabePhotoManager.App.Services;

public interface IExifLocationReader
{
    PhotoCoordinate? TryRead(string path);
}

public sealed class ExifLocationReader : IExifLocationReader
{
    public PhotoCoordinate? TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            foreach (var directory in ImageMetadataReader.ReadMetadata(path).OfType<GpsDirectory>())
            {
                if (directory.TryGetGeoLocation(out var location))
                    return Validate(location.Latitude, location.Longitude);
            }
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or ArgumentException)
        {
        }
        return null;
    }

    public static PhotoCoordinate? Validate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)) return null;
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;
        return new PhotoCoordinate(latitude, longitude);
    }
}

public sealed record PhotoCoordinate(double Latitude, double Longitude);
