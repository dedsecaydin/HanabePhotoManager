using System.IO;

namespace HanabePhotoManager.App.Watermark;

public sealed record WatermarkFolderBatchItem(string SourceRoot, string SourcePath, string RelativeDirectory);

public sealed record WatermarkFolderScanResult(
    IReadOnlyList<WatermarkFolderBatchItem> Items,
    IReadOnlyList<string> Warnings);

public sealed record WatermarkFolderBatchProgress(
    int Completed,
    int Total,
    int Success,
    int Failed,
    string CurrentFile);

public sealed record WatermarkFolderBatchResult(
    IReadOnlyList<WatermarkExportResult> Results,
    int Success,
    int Failed);

public sealed class WatermarkFolderBatchService
{
    private readonly WatermarkExportService _exporter;

    public WatermarkFolderBatchService()
        : this(new WatermarkExportService())
    {
    }

    internal WatermarkFolderBatchService(WatermarkExportService exporter)
    {
        _exporter = exporter;
    }

    public Task<WatermarkFolderScanResult> ScanAsync(
        IEnumerable<string> sourceDirectories,
        string outputRoot,
        bool recursive,
        CancellationToken token = default,
        IProgress<int>? progress = null)
    {
        var sources = sourceDirectories.ToArray();
        return Task.Run(() => Scan(sources, outputRoot, recursive, token, progress), token);
    }

    public async Task<WatermarkFolderBatchResult> ProcessAsync(
        IReadOnlyList<WatermarkFolderBatchItem> items,
        string watermarkPath,
        WatermarkExportOptions options,
        IProgress<WatermarkFolderBatchProgress>? progress = null,
        CancellationToken token = default)
    {
        var results = new List<WatermarkExportResult>(items.Count);
        var success = 0;
        var failed = 0;

        for (var index = 0; index < items.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var item = items[index];
            var outputDirectory = Path.Combine(options.OutputDirectory, item.RelativeDirectory);
            var itemOptions = options with { OutputDirectory = outputDirectory, MaxParallelism = 1 };
            var result = AssertSingle(await _exporter.ExportAsync(
                [item.SourcePath],
                watermarkPath,
                itemOptions,
                token: token).ConfigureAwait(false));
            results.Add(result);
            if (result.Status == WatermarkExportStatus.Success) success++;
            else failed++;
            progress?.Report(new(index + 1, items.Count, success, failed, Path.GetFileName(item.SourcePath)));
        }

        return new(results, success, failed);
    }

    private static WatermarkFolderScanResult Scan(
        IReadOnlyList<string> sourceDirectories,
        string outputRoot,
        bool recursive,
        CancellationToken token,
        IProgress<int>? progress)
    {
        token.ThrowIfCancellationRequested();
        var output = NormalizePath(outputRoot);
        var items = new Dictionary<string, WatermarkFolderBatchItem>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var sourceValue in sourceDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            token.ThrowIfCancellationRequested();
            var source = NormalizePath(sourceValue);
            if (!Directory.Exists(source))
            {
                warnings.Add($"来源文件夹不存在：{source}");
                continue;
            }

            if (IsHidden(source) || IsSameOrChild(source, output))
                continue;

            var pending = new Stack<string>();
            pending.Push(source);
            while (pending.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested();
                if (IsHidden(directory) || IsSameOrChild(directory, output))
                    continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        if (IsHidden(file) || !WatermarkInputDiscovery.IsSupported(file))
                            continue;

                        var fullPath = NormalizePath(file);
                        if (items.TryAdd(fullPath, new(
                            source,
                            fullPath,
                            NormalizeRelativeDirectory(Path.GetRelativePath(source, Path.GetDirectoryName(fullPath)!)))))
                        {
                            progress?.Report(items.Count);
                        }
                    }

                    if (!recursive)
                        continue;

                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        if (!IsHidden(child) && !IsSameOrChild(child, output))
                            pending.Push(child);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"无法读取：{directory}（{ex.Message}）");
                }
            }
        }

        return new(
            items.Values.OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings);
    }

    private static WatermarkExportResult AssertSingle(IReadOnlyList<WatermarkExportResult> results)
    {
        if (results.Count != 1)
            throw new InvalidOperationException("文件夹批处理单项导出未返回唯一结果。");
        return results[0];
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string NormalizeRelativeDirectory(string path) =>
        path == "." ? string.Empty : path;

    private static bool IsHidden(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsSameOrChild(string candidate, string parent)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return false;

        var normalizedCandidate = NormalizePath(candidate);
        var normalizedParent = NormalizePath(parent);
        return string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
