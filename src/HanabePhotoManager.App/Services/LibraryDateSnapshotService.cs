using System.IO;
using System.Security.Cryptography;
using System.Text;
using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

public interface ILibraryDateFileSystem
{
    bool DirectoryExists(string path);

    DateTime GetDirectoryLastWriteTimeUtc(string path);

    IEnumerable<LibraryDateFileReadResult> EnumerateTopLevelFiles(string path);

    IEnumerable<LibraryDateFileReadResult> EnumerateFilesRecursively(string path);
}

public sealed class LibraryDateSnapshotService
{
    private const int BatchSize = 64;
    private const int CacheCapacity = 3;

    public static readonly IReadOnlyList<string> DefaultCategoryNames =
    [
        "RAW生图",
        "JPG生图",
        "修后",
        "视频",
        "action视频",
        "素材"
    ];

    private static readonly HashSet<string> SupportedExtensions = new(
        [
            ".arw", ".cr2", ".cr3", ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".webp", ".heic", ".mp4", ".mov", ".xml", ".lrf", ".aac"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly ILibraryDateFileSystem _fileSystem;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();

    public LibraryDateSnapshotService(ILibraryDateFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new PhysicalLibraryDateFileSystem();
    }

    public int CachedSnapshotCount
    {
        get
        {
            lock (_cacheGate)
            {
                return _cache.Count;
            }
        }
    }

    public Task<LibraryDateSnapshot> LoadAsync(
        string dateDirectory,
        CancellationToken cancellationToken = default)
        => LoadAsync(dateDirectory, progress: null, cancellationToken);

    public Task<LibraryDateSnapshot> LoadAsync(
        string dateDirectory,
        IProgress<LibraryDateSnapshotBatch>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dateDirectory);
        var normalized = Path.GetFullPath(dateDirectory);
        return Task.Run(
            () => LoadCore(normalized, progress, cancellationToken),
            cancellationToken);
    }

    public Task<LibraryDirectoryCapacityResult> CalculateCapacityAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var normalized = Path.GetFullPath(directory);
        return Task.Run(
            () => CalculateCapacityCore(normalized, cancellationToken),
            cancellationToken);
    }

    public void Invalidate(string dateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dateDirectory);
        var normalized = Path.GetFullPath(dateDirectory);
        lock (_cacheGate)
        {
            RemoveCacheEntry(normalized);
        }
    }

    public void ClearCache()
    {
        lock (_cacheGate)
        {
            _cache.Clear();
            _lru.Clear();
        }
    }

    private LibraryDateSnapshot LoadCore(
        string dateDirectory,
        IProgress<LibraryDateSnapshotBatch>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_fileSystem.DirectoryExists(dateDirectory))
        {
            throw new DirectoryNotFoundException($"日期目录不存在：{dateDirectory}");
        }

        var startingFingerprint = TryCreateFingerprint(dateDirectory);
        if (startingFingerprint is not null &&
            TryGetCached(dateDirectory, startingFingerprint, out var cached))
        {
            ReportCachedBatches(cached, progress, cancellationToken);
            return cached;
        }

        var items = new List<LibraryDateMediaItem>();
        var categories = new List<LibraryDateCategorySnapshot>(DefaultCategoryNames.Count);
        var warnings = new List<LibraryDateSnapshotWarning>();
        var pendingBatch = new List<LibraryDateMediaItem>(BatchSize);

        foreach (var category in DefaultCategoryNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var categoryPath = Path.Combine(dateDirectory, category);
            var count = 0;
            var totalBytes = 0L;

            if (_fileSystem.DirectoryExists(categoryPath))
            {
                try
                {
                    foreach (var result in _fileSystem.EnumerateTopLevelFiles(categoryPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!result.IsSuccess)
                        {
                            warnings.Add(new LibraryDateSnapshotWarning(
                                LibraryDateSnapshotWarningKind.FileUnavailable,
                                result.FailedPath ?? categoryPath,
                                result.ErrorMessage ?? "无法读取文件属性。"));
                            continue;
                        }

                        var properties = result.File!;
                        if (!SupportedExtensions.Contains(properties.Extension))
                        {
                            continue;
                        }

                        var item = new LibraryDateMediaItem(
                            properties.FullPath,
                            properties.Name,
                            properties.Extension.TrimStart('.').ToUpperInvariant(),
                            category,
                            properties.Length,
                            properties.LastWriteTimeUtc);
                        items.Add(item);
                        pendingBatch.Add(item);
                        count++;
                        totalBytes += item.Length;

                        if (pendingBatch.Count == BatchSize)
                        {
                            progress?.Report(new LibraryDateSnapshotBatch(
                                pendingBatch.ToArray(),
                                items.Count,
                                false));
                            pendingBatch.Clear();
                        }
                    }
                }
                catch (Exception ex) when (IsRecoverableFileSystemException(ex))
                {
                    warnings.Add(new LibraryDateSnapshotWarning(
                        LibraryDateSnapshotWarningKind.CategoryUnavailable,
                        categoryPath,
                        ex.Message));
                }
            }

            categories.Add(new LibraryDateCategorySnapshot(
                category,
                categoryPath,
                count,
                totalBytes));
        }

        if (pendingBatch.Count > 0)
        {
            progress?.Report(new LibraryDateSnapshotBatch(
                pendingBatch.ToArray(),
                items.Count,
                false));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var endingFingerprint = TryCreateFingerprint(dateDirectory);
        if (startingFingerprint is not null &&
            endingFingerprint is not null &&
            !string.Equals(
                startingFingerprint,
                endingFingerprint,
                StringComparison.Ordinal))
        {
            warnings.Add(new LibraryDateSnapshotWarning(
                LibraryDateSnapshotWarningKind.DirectoryChangedDuringScan,
                dateDirectory,
                "扫描期间目录发生变化，结果未缓存。"));
        }

        var fingerprint = endingFingerprint ?? startingFingerprint ?? string.Empty;
        var snapshot = new LibraryDateSnapshot(
            dateDirectory,
            items.ToArray(),
            categories.ToArray(),
            warnings.ToArray(),
            warnings.Count > 0,
            fingerprint,
            DateTime.UtcNow);

        if (!snapshot.IsPartial &&
            startingFingerprint is not null &&
            endingFingerprint is not null)
        {
            AddToCache(dateDirectory, endingFingerprint, snapshot);
        }

        return snapshot;
    }

    private LibraryDirectoryCapacityResult CalculateCapacityCore(
        string directory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_fileSystem.DirectoryExists(directory))
        {
            throw new DirectoryNotFoundException($"目录不存在：{directory}");
        }

        var warnings = new List<LibraryDateSnapshotWarning>();
        var totalBytes = 0L;
        var filesVisited = 0;
        try
        {
            foreach (var result in _fileSystem.EnumerateFilesRecursively(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.IsSuccess)
                {
                    warnings.Add(new LibraryDateSnapshotWarning(
                        LibraryDateSnapshotWarningKind.CapacityEntryUnavailable,
                        result.FailedPath ?? directory,
                        result.ErrorMessage ?? "无法读取文件属性。"));
                    continue;
                }

                totalBytes += result.File!.Length;
                filesVisited++;
            }
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            warnings.Add(new LibraryDateSnapshotWarning(
                LibraryDateSnapshotWarningKind.CapacityEntryUnavailable,
                directory,
                ex.Message));
        }

        return new LibraryDirectoryCapacityResult(
            directory,
            totalBytes,
            filesVisited,
            warnings.ToArray(),
            warnings.Count > 0);
    }

    private string? TryCreateFingerprint(string dateDirectory)
    {
        try
        {
            var builder = new StringBuilder(dateDirectory);
            AppendDirectoryStamp(builder, dateDirectory);
            foreach (var category in DefaultCategoryNames)
            {
                AppendDirectoryStamp(builder, Path.Combine(dateDirectory, category));
            }

            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
        }
        catch (Exception ex) when (IsRecoverableFileSystemException(ex))
        {
            return null;
        }
    }

    private void AppendDirectoryStamp(StringBuilder builder, string path)
    {
        builder.Append('|').Append(path).Append(':');
        if (_fileSystem.DirectoryExists(path))
        {
            builder.Append(_fileSystem.GetDirectoryLastWriteTimeUtc(path).Ticks);
        }
        else
        {
            builder.Append("missing");
        }
    }

    private bool TryGetCached(
        string path,
        string fingerprint,
        out LibraryDateSnapshot snapshot)
    {
        lock (_cacheGate)
        {
            if (_cache.TryGetValue(path, out var entry) &&
                string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
                snapshot = entry.Snapshot;
                return true;
            }

            RemoveCacheEntry(path);
            snapshot = null!;
            return false;
        }
    }

    private void AddToCache(
        string path,
        string fingerprint,
        LibraryDateSnapshot snapshot)
    {
        lock (_cacheGate)
        {
            RemoveCacheEntry(path);
            var node = _lru.AddFirst(path);
            _cache[path] = new CacheEntry(fingerprint, snapshot, node);

            while (_cache.Count > CacheCapacity)
            {
                var oldest = _lru.Last;
                if (oldest is null)
                {
                    break;
                }

                RemoveCacheEntry(oldest.Value);
            }
        }
    }

    private void RemoveCacheEntry(string path)
    {
        if (!_cache.Remove(path, out var removed))
        {
            return;
        }

        _lru.Remove(removed.Node);
    }

    private static void ReportCachedBatches(
        LibraryDateSnapshot snapshot,
        IProgress<LibraryDateSnapshotBatch>? progress,
        CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return;
        }

        for (var offset = 0; offset < snapshot.Items.Count; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = snapshot.Items
                .Skip(offset)
                .Take(BatchSize)
                .ToArray();
            progress.Report(new LibraryDateSnapshotBatch(
                items,
                Math.Min(offset + items.Length, snapshot.Items.Count),
                true));
        }
    }

    private static bool IsRecoverableFileSystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private sealed record CacheEntry(
        string Fingerprint,
        LibraryDateSnapshot Snapshot,
        LinkedListNode<string> Node);
}

public sealed class PhysicalLibraryDateFileSystem : ILibraryDateFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public DateTime GetDirectoryLastWriteTimeUtc(string path) =>
        Directory.GetLastWriteTimeUtc(path);

    public IEnumerable<LibraryDateFileReadResult> EnumerateTopLevelFiles(string path) =>
        EnumerateFiles(path, SearchOption.TopDirectoryOnly);

    public IEnumerable<LibraryDateFileReadResult> EnumerateFilesRecursively(string path) =>
        EnumerateFiles(path, SearchOption.AllDirectories);

    private static IEnumerable<LibraryDateFileReadResult> EnumerateFiles(
        string path,
        SearchOption searchOption)
    {
        foreach (var info in new DirectoryInfo(path).EnumerateFiles("*", searchOption))
        {
            LibraryDateFileReadResult result;
            try
            {
                result = LibraryDateFileReadResult.Success(
                    new LibraryDateFileProperties(
                        info.FullName,
                        info.Name,
                        info.Extension,
                        info.Length,
                        info.LastWriteTimeUtc));
            }
            catch (Exception ex) when (IsRecoverableFileSystemException(ex))
            {
                result = LibraryDateFileReadResult.Failure(info.FullName, ex.Message);
            }

            yield return result;
        }
    }

    private static bool IsRecoverableFileSystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
