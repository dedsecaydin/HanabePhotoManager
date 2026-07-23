using System.IO;
using MetadataExtractor;

namespace HanabePhotoManager.App.Services;

public interface IPhotoDetailMetadataReader
{
    PhotoDetailMetadata Read(string path);
}

public sealed record PhotoDetailMetadata(
    string Path,
    string Name,
    string Extension,
    string FileSize,
    string Dimensions,
    string TakenAt,
    string Iso,
    string Aperture,
    string Shutter,
    string FocalLength,
    string Camera,
    string Lens,
    string Location)
{
    public static PhotoDetailMetadata Empty(string path) => new(
        path, System.IO.Path.GetFileNameWithoutExtension(path),
        System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
        "未记录", "未记录", "未记录", "未记录", "未记录", "未记录", "未记录", "未记录", "未记录", "未记录");
}

public sealed class PhotoDetailMetadataReader : IPhotoDetailMetadataReader
{
    private readonly IExifLocationReader _locationReader;
    public PhotoDetailMetadataReader(IExifLocationReader? locationReader = null) =>
        _locationReader = locationReader ?? new ExifLocationReader();

    public PhotoDetailMetadata Read(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var empty = PhotoDetailMetadata.Empty(fullPath);
        var size = File.Exists(fullPath) ? FormatBytes(new FileInfo(fullPath).Length) : "未记录";
        IReadOnlyList<MetadataExtractor.Directory> directories = [];
        try { directories = ImageMetadataReader.ReadMetadata(fullPath); }
        catch (Exception ex) when (ex is IOException or ImageProcessingException or UnauthorizedAccessException) { }

        string Find(params string[] names)
        {
            var tag = directories.SelectMany(directory => directory.Tags)
                .FirstOrDefault(candidate => names.Any(name => candidate.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
            return string.IsNullOrWhiteSpace(tag?.Description) ? "未记录" : tag.Description!;
        }

        var width = Find("Image Width", "Exif Image Width", "Pixel X Dimension");
        var height = Find("Image Height", "Exif Image Height", "Pixel Y Dimension");
        var dimensions = width == "未记录" || height == "未记录" ? "未记录" : $"{width} × {height}";
        string location = "未记录";
        try
        {
            if (_locationReader.TryRead(fullPath) is { } coordinate)
                location = $"{coordinate.Latitude:F6}, {coordinate.Longitude:F6}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return empty with
        {
            FileSize = size,
            Dimensions = dimensions,
            TakenAt = Find("Date/Time Original", "Date Time Original", "Date/Time"),
            Iso = Find("ISO Speed Ratings", "ISO Equivalent", "ISO"),
            Aperture = Find("F-Number", "Aperture Value"),
            Shutter = Find("Exposure Time", "Shutter Speed"),
            FocalLength = Find("Focal Length"),
            Camera = Join(Find("Make"), Find("Model")),
            Lens = Find("Lens Model", "Lens Specification", "Lens"),
            Location = location
        };
    }

    private static string Join(string first, string second) => (first, second) switch
    {
        ("未记录", "未记录") => "未记录",
        ("未记录", _) => second,
        (_, "未记录") => first,
        _ => $"{first} {second}"
    };

    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:F2} MB"
        : $"{Math.Max(1, bytes / 1024d):F1} KB";
}
