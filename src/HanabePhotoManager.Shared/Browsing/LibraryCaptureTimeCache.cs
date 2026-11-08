using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Infrastructure.Sessions;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;

namespace HanabePhotoManager.App.Browsing;

/// <summary>
/// 已导入文件的「真实拍摄时间」缓存（%AppData%/capture-times.json，按 路径 + 大小 + 修改时间 失效）。
/// 导入复制后文件的修改时间是导入时刻，不能拿来分场；这里读 EXIF（照片）、Sony XML / QuickTime 创建时间（视频）。
/// 读不到时间的记为 null（不参与分场计时）。
/// </summary>
public sealed class LibraryCaptureTimeCache
{
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".m4v", ".mts", ".m2ts", ".avi", ".mxf" };
    private static readonly Regex SonyClipAtEnd = new(@"(?<=^|[(（])(C\d+)(?=[)）]?$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex XmlCreationDate = new("<CreationDate\\s+value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly string _path;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private int _dirty;

    public sealed record Entry(long Length, long WriteTicks, DateTime? Time);

    public LibraryCaptureTimeCache(string? path = null)
    {
        _path = path ?? Path.Combine(AppDataPaths.Root, "capture-times.json");
        try
        {
            if (File.Exists(_path) &&
                JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(_path)) is { } stored)
                foreach (var (key, value) in stored) _entries[key] = value;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }

    /// <summary>缓存里有这个文件（同大小、同修改时间）时返回 true；time 为 null 表示读过但没有拍摄时间。</summary>
    public bool TryGet(string path, long length, DateTime writeTime, out DateTime? time)
    {
        time = null;
        if (!_entries.TryGetValue(path, out var entry) || entry.Length != length || entry.WriteTicks != writeTime.Ticks) return false;
        time = entry.Time;
        return true;
    }

    public void Set(string path, long length, DateTime writeTime, DateTime? time)
    {
        _entries[path] = new Entry(length, writeTime.Ticks, time);
        Interlocked.Exchange(ref _dirty, 1);
    }

    public void Save()
    {
        if (Interlocked.Exchange(ref _dirty, 0) == 0) return;
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_entries.ToDictionary(pair => pair.Key, pair => pair.Value)));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>读一个文件的拍摄时间（相机本地时间）。读不到返回 null。</summary>
    public static DateTime? ReadCaptureTime(string path)
    {
        try
        {
            return Videos.Contains(Path.GetExtension(path)) ? ReadVideoTime(path) : ReadPhotoTime(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageProcessingException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static DateTime? ReadPhotoTime(string path) => new ExifCaptureTimeReader().Read(path).LocalTime;

    private static DateTime? ReadVideoTime(string path)
    {
        // Sony：同目录的 C3858M01.XML / 「JK0166（C3858M01）.XML」里有带时区的 CreationDate（相机本地时间）
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        if (SonyClipAtEnd.IsMatch(stem))
        {
            var xmlStem = SonyClipAtEnd.Replace(stem, "${1}M01");
            foreach (var extension in new[] { ".XML", ".xml" })
            {
                var xml = Path.Combine(directory, xmlStem + extension);
                if (!File.Exists(xml)) continue;
                var match = XmlCreationDate.Match(File.ReadAllText(xml));
                if (match.Success && DateTimeOffset.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
                    return created.DateTime;
            }
        }

        // 通用：QuickTime 影片头的创建时间（UTC）→ 本机时间
        var movie = ImageMetadataReader.ReadMetadata(path).OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        if (movie is not null && movie.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var utc) && utc.Year > 1990)
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return null;
    }
}
