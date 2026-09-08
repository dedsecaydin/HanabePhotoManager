using System.IO;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Services;

public sealed record LibraryHealthIssue(string Path, string Kind, string Detail);
public sealed record LibraryHealthReport(DateTimeOffset CheckedAt, int CheckedFiles, int DecodedImages,
    IReadOnlyList<LibraryHealthIssue> Issues);

/// <summary>Checks an indexed snapshot without modifying media or following filesystem links.</summary>
public sealed class LibraryHealthService
{
    private static readonly HashSet<string> Raster = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };
    private static readonly HashSet<string> Raw = new(StringComparer.OrdinalIgnoreCase) { ".arw", ".cr2", ".cr3", ".nef", ".dng", ".raf", ".orf", ".rw2" };

    public Task<LibraryHealthReport> CheckAsync(IEnumerable<string> indexedPaths, bool decodeImages,
        IProgress<double>? progress, CancellationToken token)
    {
        var paths = indexedPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.Run(() => Check(paths, decodeImages, progress, token), token);
    }

    private static LibraryHealthReport Check(string[] paths, bool decodeImages, IProgress<double>? progress, CancellationToken token)
    {
        var issues = new List<LibraryHealthIssue>(); int checkedFiles = 0, decoded = 0;
        var jpegKeys = paths.Where(p => Path.GetExtension(p).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(p).Equals(".jpeg", StringComparison.OrdinalIgnoreCase)).Select(PairKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) issues.Add(new(path, "文件缺失", "索引中的路径不存在或暂时不可访问；请检查设备连接后刷新照片库。"));
                else if ((info.Attributes & FileAttributes.ReparsePoint) != 0) issues.Add(new(path, "跳过链接", "未跟随文件系统链接。"));
                else if (info.Length == 0) issues.Add(new(path, "空文件", "文件大小为 0，无法包含完整媒体。"));
                else
                {
                    using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (source.ReadByte() < 0) throw new EndOfStreamException();
                    source.Position = 0;
                    if (decodeImages && Raster.Contains(info.Extension))
                    {
                        var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                        if (decoder.Frames.Count != 1) issues.Add(new(path, "多帧文件", "仅验证第一帧；其他帧尚未检查。"));
                        var frame = decoder.Frames[0];
                        if ((long)frame.PixelWidth * frame.PixelHeight > 32_000_000)
                            issues.Add(new(path, "跳过大图解码", "超过 3200 万像素，仅确认可读取，防止检查占用过多内存。"));
                        else
                        {
                            int stride = checked((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8);
                            var pixels = new byte[checked(stride * frame.PixelHeight)];
                            frame.CopyPixels(pixels, stride, 0); decoded++;
                        }
                    }
                    if (Raw.Contains(info.Extension) && !jpegKeys.Contains(PairKey(path)))
                        issues.Add(new(path, "配对提示", "索引中未找到同名 JPEG；如果相机仅拍 RAW，这是正常情况。"));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or
                System.Runtime.InteropServices.COMException or ArgumentException or OverflowException or FileFormatException)
            { issues.Add(new(path, "读取或解码失败", ex.Message)); }
            checkedFiles++; progress?.Report(checkedFiles * 100d / Math.Max(1, paths.Length));
        }
        return new(DateTimeOffset.Now, checkedFiles, decoded, issues);
    }

    private static string PairKey(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var category = Path.GetFileName(directory);
        if (category is "RAW生图" or "JPG生图") directory = Path.GetDirectoryName(directory) ?? directory;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(path));
    }
}
