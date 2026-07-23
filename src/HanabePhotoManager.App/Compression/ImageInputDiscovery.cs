using System.IO;

namespace HanabePhotoManager.App.Compression;

public sealed record ImageDiscoveryResult(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

public sealed class ImageInputDiscovery
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff",
        ".heic", ".heif", ".avif", ".arw", ".cr2", ".cr3", ".nef", ".dng", ".raf", ".orf", ".rw2"
    };

    public ImageDiscoveryResult Discover(IEnumerable<string> inputs, bool recursive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var input in inputs.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try { fullPath = Path.GetFullPath(input); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                warnings.Add($"无法识别路径：{input}");
                continue;
            }

            if (File.Exists(fullPath))
            {
                if (IsSupported(fullPath)) files.Add(fullPath);
                else warnings.Add($"不支持的图片格式：{fullPath}");
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                warnings.Add($"文件或文件夹不存在：{fullPath}");
                continue;
            }

            ScanDirectory(fullPath, recursive, files, warnings, cancellationToken);
        }

        return new ImageDiscoveryResult(files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }

    public static bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private static void ScanDirectory(
        string root,
        bool recursive,
        HashSet<string> files,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsSupported(file)) files.Add(Path.GetFullPath(file));
                }

                if (!recursive) continue;
                foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PathTooLongException)
            {
                warnings.Add($"无法读取文件夹：{directory}（{ex.Message}）");
            }
        }
    }
}
