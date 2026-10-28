using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using HanabePhotoManager.Core.Sessions;
namespace HanabePhotoManager.Infrastructure.Sessions;
public sealed class ExifCaptureTimeReader : ICaptureTimeReader
{
    public CaptureMember Read(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(full); var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault(); var ifd = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            var camera = string.Join(" ", new[] { ifd?.GetString(ExifDirectoryBase.TagMake), ifd?.GetString(ExifDirectoryBase.TagModel), exif?.GetString(0xA431) }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var raw = exif?.GetString(ExifDirectoryBase.TagDateTimeOriginal);
            if (!DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return new(full, full, null, null, camera, "缺失或无效的 EXIF DateTimeOriginal");
            var sub = exif?.GetString(0x9291); if (!string.IsNullOrEmpty(sub) && sub.All(char.IsAsciiDigit)) time = time.AddTicks(long.Parse(sub.PadRight(7, '0')[..7], CultureInfo.InvariantCulture));
            TimeSpan? offset = null; var offsetText = exif?.GetString(0x9011);
            if (!string.IsNullOrWhiteSpace(offsetText)) { if (offsetText.Length != 6 || (offsetText[0] != (char)43 && offsetText[0] != (char)45) || !TimeSpan.TryParseExact(offsetText.TrimStart('+', '-'), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed) || parsed > TimeSpan.FromHours(14)) return new(full, full, DateTime.SpecifyKind(time, DateTimeKind.Unspecified), null, camera, "无效 EXIF 时区偏移"); offset = offsetText.StartsWith('-') ? -parsed : parsed; }
            return new(full, full, DateTime.SpecifyKind(time, DateTimeKind.Unspecified), offset, camera);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ImageProcessingException or FormatException) { return new(full, full, null, null, "", e.Message); }
    }
}
