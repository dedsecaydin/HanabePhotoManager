using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace HanabePhotoManager.App.Services;

/// <summary>定义从照片元数据读取 GPS 坐标的边界。</summary>
public interface IExifLocationReader
{
    PhotoCoordinate? TryRead(string path);
}

/// <summary>使用 MetadataExtractor 读取并验证照片 EXIF GPS 坐标。</summary>
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

/// <summary>十进制度表示的有效地理坐标。</summary>
public sealed record PhotoCoordinate(double Latitude, double Longitude);
