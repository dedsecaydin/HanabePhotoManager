using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

public sealed class FileCloudCacheStore : ICloudCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _root;
    private readonly string _contentRoot;
    private readonly string _indexPath;
    private readonly string _lockPath;
    private readonly Func<DateTimeOffset> _utcNow;

    public FileCloudCacheStore(string root, Func<DateTimeOffset> utcNow)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Cache root is required.", nameof(root));
        }

        ArgumentNullException.ThrowIfNull(utcNow);

        _root = Path.GetFullPath(root);
        _contentRoot = Path.Combine(_root, "content");
        _indexPath = Path.Combine(_root, "cache-index.json");
        _lockPath = _indexPath + ".lock";
        _utcNow = utcNow;
    }

    public async Task<string?> TryGetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_root);

        await using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var entries = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        var contentPath = ResolveEntryPath(entry);
        if (!File.Exists(contentPath) || new FileInfo(contentPath).Length != entry.Size)
        {
            entries.Remove(key);
            await SaveIndexAsync(entries.Values, cancellationToken).ConfigureAwait(false);
            return null;
        }

        entries[key] = entry with { LastAccessedAt = _utcNow() };
        await SaveIndexAsync(entries.Values, cancellationToken).ConfigureAwait(false);
        return contentPath;
    }

    public async Task<string> PutAsync(
        string key,
        Stream content,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("Cache content stream must be readable.", nameof(content));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_contentRoot);

        await using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var entries = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
        var contentPath = GetContentPath(key);
        var relativePath = GetRelativePath(contentPath);
        var contentTemporaryPath = CreateTemporaryPath(_contentRoot, Path.GetFileName(contentPath));
        string? indexTemporaryPath = null;
        string? rollbackPath = null;
        var contentCommitted = false;
        var indexCommitted = false;

        try
        {
            long size;
            await using (var destination = new FileStream(
                contentTemporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                destination.Flush(flushToDisk: true);
                size = destination.Position;
            }

            entries[key] = new CacheEntry(key, relativePath, size, _utcNow(), pinned);
            indexTemporaryPath = await WriteIndexTemporaryAsync(entries.Values, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(contentPath))
            {
                rollbackPath = Path.Combine(
                    _contentRoot,
                    $"{Path.GetFileName(contentPath)}.{Guid.NewGuid():N}.rollback");
                File.Move(contentPath, rollbackPath);
            }

            try
            {
                File.Move(contentTemporaryPath, contentPath);
                contentCommitted = true;
                File.Move(indexTemporaryPath, _indexPath, overwrite: true);
                indexCommitted = true;
            }
            catch
            {
                if (contentCommitted && File.Exists(contentPath))
                {
                    File.Delete(contentPath);
                }

                if (rollbackPath is not null && File.Exists(rollbackPath))
                {
                    File.Move(rollbackPath, contentPath);
                }

                throw;
            }

            return contentPath;
        }
        finally
        {
            DeleteIfExists(contentTemporaryPath);
            if (!indexCommitted && indexTemporaryPath is not null)
            {
                DeleteIfExists(indexTemporaryPath);
            }

            if (rollbackPath is not null && File.Exists(rollbackPath))
            {
                DeleteIfExists(rollbackPath);
            }
        }
    }

    public async Task TrimAsync(
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        if (maximumBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum cache size cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_root);

        await using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        var entries = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
        decimal total = entries.Values.Sum(static entry => (decimal)entry.Size);
        if (total <= maximumBytes)
        {
            return;
        }

        var removed = new List<CacheEntry>();
        foreach (var entry in entries.Values
                     .Where(static entry => !entry.Pinned)
                     .OrderBy(static entry => entry.LastAccessedAt)
                     .ThenBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            entries.Remove(entry.Key);
            removed.Add(entry);
            total -= entry.Size;
            if (total <= maximumBytes)
            {
                break;
            }
        }

        if (removed.Count == 0)
        {
            return;
        }

        var indexTemporaryPath = await WriteIndexTemporaryAsync(entries.Values, cancellationToken)
            .ConfigureAwait(false);
        var movedFiles = new List<(string Original, string Rollback)>();
        var indexCommitted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in removed)
            {
                var original = ResolveEntryPath(entry);
                if (!File.Exists(original))
                {
                    continue;
                }

                var rollback = Path.Combine(
                    _contentRoot,
                    $"{Path.GetFileName(original)}.{Guid.NewGuid():N}.rollback");
                File.Move(original, rollback);
                movedFiles.Add((original, rollback));
            }

            try
            {
                File.Move(indexTemporaryPath, _indexPath, overwrite: true);
                indexCommitted = true;
            }
            catch
            {
                RestoreMovedFiles(movedFiles);
                throw;
            }
        }
        catch
        {
            if (!indexCommitted)
            {
                RestoreMovedFiles(movedFiles);
            }

            throw;
        }
        finally
        {
            if (!indexCommitted)
            {
                DeleteIfExists(indexTemporaryPath);
            }

            if (indexCommitted)
            {
                foreach (var (_, rollback) in movedFiles)
                {
                    DeleteIfExists(rollback);
                }
            }
        }
    }

    private async Task<Dictionary<string, CacheEntry>> LoadIndexAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
        {
            return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = new FileStream(
                _indexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var storedEntries = await JsonSerializer.DeserializeAsync<StoredCacheEntry?[]>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (storedEntries is null)
            {
                throw new InvalidDataException("The cache index is not a JSON array.");
            }

            var entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
            var paths = new HashSet<string>(PathComparer);
            foreach (var stored in storedEntries)
            {
                var entry = ToEntry(stored);
                var path = ResolveEntryPath(entry);
                if (!entries.TryAdd(entry.Key, entry))
                {
                    throw new InvalidDataException($"Duplicate cache key '{entry.Key}'.");
                }

                if (!paths.Add(path))
                {
                    throw new InvalidDataException($"Duplicate cache path '{entry.RelativePath}'.");
                }
            }

            return entries;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or
                                           ArgumentException or NotSupportedException or IOException)
        {
            throw new InvalidDataException(
                $"Cloud cache index '{_indexPath}' contains invalid data.",
                exception);
        }
    }

    private async Task SaveIndexAsync(
        IEnumerable<CacheEntry> entries,
        CancellationToken cancellationToken)
    {
        var temporaryPath = await WriteIndexTemporaryAsync(entries, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _indexPath, overwrite: true);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
        }
    }

    private async Task<string> WriteIndexTemporaryAsync(
        IEnumerable<CacheEntry> entries,
        CancellationToken cancellationToken)
    {
        var temporaryPath = CreateTemporaryPath(_root, Path.GetFileName(_indexPath));
        try
        {
            var storedEntries = entries
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => new StoredCacheEntry(
                    entry.Key,
                    entry.RelativePath,
                    entry.Size,
                    entry.LastAccessedAt,
                    entry.Pinned))
                .ToArray();
            await using var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(
                    stream,
                    storedEntries,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            stream.Flush(flushToDisk: true);
            return temporaryPath;
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }
    }

    private CacheEntry ToEntry(StoredCacheEntry? stored)
    {
        if (stored is null)
        {
            throw new InvalidDataException("The cache index contains a null entry.");
        }

        var key = RequireReference(stored.Key, "key");
        ValidateKey(key);
        var relativePath = RequireReference(stored.RelativePath, "relativePath");
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("Cache relativePath cannot be blank.");
        }

        var size = RequireValue(stored.Size, "size");
        if (size < 0)
        {
            throw new InvalidDataException("Cache size cannot be negative.");
        }

        var entry = new CacheEntry(
            key,
            relativePath,
            size,
            RequireValue(stored.LastAccessedAt, "lastAccessedAt"),
            RequireValue(stored.Pinned, "pinned"));
        ResolveEntryPath(entry);
        return entry;
    }

    private string ResolveEntryPath(CacheEntry entry)
    {
        if (Path.IsPathRooted(entry.RelativePath))
        {
            throw new InvalidDataException("Cache relative paths cannot be rooted.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, entry.RelativePath));
        if (!IsContainedBy(fullPath, _contentRoot))
        {
            throw new InvalidDataException(
                $"Cache path '{entry.RelativePath}' escapes the content directory.");
        }

        var expected = GetContentPath(entry.Key);
        if (!string.Equals(fullPath, expected, PathComparison))
        {
            throw new InvalidDataException(
                $"Cache path '{entry.RelativePath}' does not match its key hash.");
        }

        return fullPath;
    }

    private string GetContentPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        var fullPath = Path.GetFullPath(Path.Combine(_contentRoot, hash));
        if (!IsContainedBy(fullPath, _contentRoot))
        {
            throw new InvalidDataException("Computed cache path escaped the content directory.");
        }

        return fullPath;
    }

    private string GetRelativePath(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
        var verified = Path.GetFullPath(Path.Combine(_root, relative));
        if (!IsContainedBy(verified, _contentRoot))
        {
            throw new InvalidDataException("Computed cache relative path escaped the content directory.");
        }

        return relative;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException exception) when (IsLockContention(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsLockContention(IOException exception)
    {
        var nativeErrorCode = exception.HResult & 0xFFFF;
        return nativeErrorCode is 32 or 33;
    }

    private static void RestoreMovedFiles(IEnumerable<(string Original, string Rollback)> movedFiles)
    {
        foreach (var (original, rollback) in movedFiles.Reverse())
        {
            if (File.Exists(rollback) && !File.Exists(original))
            {
                File.Move(rollback, original);
            }
        }
    }

    private static string CreateTemporaryPath(string directory, string baseName) =>
        Path.Combine(directory, $"{baseName}.{Guid.NewGuid():N}.tmp");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsContainedBy(string path, string directory)
    {
        var normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, PathComparison);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key is required.", nameof(key));
        }
    }

    private static T RequireReference<T>(T? value, string fieldName)
        where T : class =>
        value ?? throw new InvalidDataException($"Required cache field '{fieldName}' is missing.");

    private static T RequireValue<T>(T? value, string fieldName)
        where T : struct =>
        value ?? throw new InvalidDataException($"Required cache field '{fieldName}' is missing.");

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record CacheEntry(
        string Key,
        string RelativePath,
        long Size,
        DateTimeOffset LastAccessedAt,
        bool Pinned);

    private sealed record StoredCacheEntry(
        string? Key,
        string? RelativePath,
        long? Size,
        DateTimeOffset? LastAccessedAt,
        bool? Pinned);
}
