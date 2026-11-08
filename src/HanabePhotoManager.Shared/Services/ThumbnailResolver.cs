using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HanabePhotoManager.Core.Performance;

namespace HanabePhotoManager.App.Services;

public interface IThumbnailBackend
{
    object? Decode(string path, int width);
    object? Shell(string path, int width, bool allowExtraction);
}

public sealed class ThumbnailResolver(IThumbnailBackend backend)
{
    private static readonly HashSet<string> Candidates = new([".arw", ".cr2", ".cr3", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif", ".hif", ".psd", ".psb", ".mp4", ".mov"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Videos = new([".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".mxf", ".mts", ".m2ts", ".ts", ".webm", ".mpeg", ".mpg", ".3gp", ".flv", ".ogv", ".m2t", ".mod"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Raw = new([".arw", ".cr2", ".cr3", ".nef", ".raf", ".rw2", ".orf", ".dng", ".raw"], StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _order = new();

    public object? Load(string path, int width = 260)
    {
        var extension = Path.GetExtension(path);
        if (Videos.Contains(extension) && FindVideoPoster(path) is { } poster && Load(poster, width) is { } posterImage) return posterImage;
        if (!Candidates.Contains(extension)) return null;
        var key = CacheKey(path, width);
        if (key is not null && _cache.TryGetValue(key, out var cached)) return cached;
        var image = backend.Decode(path, width);
        if (image is null && Raw.Contains(extension))
        {
            var jpg = FindPairedJpg(path);
            image = jpg is null ? null : backend.Decode(jpg, width);
            return Remember(key, image);
        }
        if (image is null && Videos.Contains(extension) && VideoFrameThumbnail.TryGetFirstFrameJpeg(path) is { } frame)
            image = backend.Decode(frame, width);
        image ??= backend.Shell(path, width, Videos.Contains(extension));
        return Remember(key, image);
    }

    public void Invalidate(string path)
    {
        var prefix = Path.GetFullPath(path) + "|";
        foreach (var key in _cache.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) _cache.TryRemove(key, out _);
    }

    private object? Remember(string? key, object? image)
    {
        if (key is null || image is null) return image;
        if (_cache.TryAdd(key, image)) _order.Enqueue(key); else _cache[key] = image;
        while (_cache.Count > PreviewLoadingPolicy.ThumbnailCacheLimit && _order.TryDequeue(out var oldest)) _cache.TryRemove(oldest, out _);
        return image;
    }

    // Sony XAVC 卡里每段视频带一张封面：C3858.MP4 ↔ THMBNL\C3858T01.JPG。导入改名后会变成
    // 「JK0166（C3858）.MP4」↔「JK0166（C3858T01）.JPG」，括号（全角/半角）里的部分同样按封面配对。
    private static readonly Regex SonyClipStem = new(@"(?<=^|[(（])(C\d+)(?=[)）]?$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SonyPosterStem = new(@"(?<=^|[(（])(C\d+)T\d{2}(?=[)）]?$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>视频文件名（不含扩展名）对应的 Sony 封面文件名；不是 Sony 片段命名时返回 null。</summary>
    internal static string? SonyPosterStemFor(string videoStem) =>
        SonyClipStem.IsMatch(videoStem) ? SonyClipStem.Replace(videoStem, "${1}T01") : null;

    /// <summary>Sony 封面文件名还原成视频文件名（去掉 T01）；不是封面命名时原样返回。</summary>
    internal static string VideoStemForSonyPoster(string posterStem) => SonyPosterStem.Replace(posterStem, "$1");

    internal static string? FindVideoPoster(string videoPath)
    {
        var categoryDirectory = Path.GetDirectoryName(videoPath);
        var dateDirectory = string.IsNullOrWhiteSpace(categoryDirectory) ? null : Directory.GetParent(categoryDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(dateDirectory)) return null;
        var stem = Path.GetFileNameWithoutExtension(videoPath);
        foreach (var folder in new[] { categoryDirectory!, Path.Combine(dateDirectory, "JPG生图"), Path.Combine(dateDirectory, "素材") })
        foreach (var posterStem in SonyPosterStemFor(stem) is { } sony ? new[] { stem, sony } : new[] { stem })
        foreach (var extension in new[] { ".JPG", ".jpg", ".JPEG", ".jpeg" })
        {
            var candidate = Path.Combine(folder, posterStem + extension);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindPairedJpg(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var stem = Path.GetFileNameWithoutExtension(path);
        return new[] { ".jpg", ".JPG", ".jpeg", ".JPEG" }.Select(ext => Path.Combine(directory, stem + ext)).FirstOrDefault(File.Exists);
    }

    private static string? CacheKey(string path, int width)
    {
        try { var info = new FileInfo(path); return info.Exists ? $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{width}" : null; }
        catch { return null; }
    }
}
