using System.IO;

namespace HanabePhotoManager.App.Watermark;

public sealed record WatermarkDiscoveryResult(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

public sealed class WatermarkInputDiscovery
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff" };

    public WatermarkDiscoveryResult Discover(IEnumerable<string> inputs, bool recursive, CancellationToken token = default)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        foreach (var input in inputs.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            token.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(input);
            if (File.Exists(path)) { if (IsSupported(path)) files.Add(path); else warnings.Add($"不支持的格式：{path}"); continue; }
            if (!Directory.Exists(path)) { warnings.Add($"路径不存在：{path}"); continue; }
            Scan(path, recursive, files, warnings, token);
        }
        return new(files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }

    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path));

    private static void Scan(string root, bool recursive, HashSet<string> files, List<string> warnings, CancellationToken token)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var dir))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir)) if (IsSupported(file)) files.Add(Path.GetFullPath(file));
                if (recursive) foreach (var child in Directory.EnumerateDirectories(dir)) pending.Push(child);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { warnings.Add($"无法读取：{dir}（{ex.Message}）"); }
        }
    }
}
