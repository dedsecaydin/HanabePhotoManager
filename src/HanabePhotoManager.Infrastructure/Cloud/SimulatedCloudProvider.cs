using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>
/// A deterministic, local-disk cloud provider used by the app while real cloud
/// connectors are being configured.  Cloud paths are always rooted below the
/// configured directory; no caller supplied path is allowed to escape it.
/// </summary>
public sealed class SimulatedCloudProvider : ICloudProvider
{
    private const int CopyBufferSize = 1024 * 1024;
    private readonly string _remoteRoot;
    private readonly long _capacityBytes;
    private readonly SemaphoreSlim _transferGate = new(1, 1);
    private readonly ConcurrentDictionary<string, UploadSession> _uploadSessions = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly StringComparison _pathComparison;

    public SimulatedCloudProvider(string remoteRoot, long capacityBytes)
    {
        if (string.IsNullOrWhiteSpace(remoteRoot))
            throw new ArgumentException("Remote root is required.", nameof(remoteRoot));
        if (capacityBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes), capacityBytes, "Capacity cannot be negative.");

        try
        {
            _remoteRoot = Path.GetFullPath(remoteRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("Remote root is not a valid local path.", nameof(remoteRoot), ex);
        }

        Directory.CreateDirectory(_remoteRoot);
        if (File.Exists(_remoteRoot))
            throw new ArgumentException("Remote root must be a directory.", nameof(remoteRoot));

        _capacityBytes = capacityBytes;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        EnsureNoReparsePoints(_remoteRoot);
    }

    public CloudProviderKind Kind => CloudProviderKind.Simulated;

    public async Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t0 = sw.ElapsedMilliseconds;
            var usedBytes = GetUsedBytes(cancellationToken);
            var t1 = sw.ElapsedMilliseconds;
            var account = new CloudAccountState(
                Kind,
                true,
                "本地模拟云盘",
                usedBytes,
                _capacityBytes,
                "已连接（本地模拟）");
            var t2 = sw.ElapsedMilliseconds;
            await WriteDiagnosticsAsync($"GetAccountStateAsync 锁等待 {t0}ms · GetUsedBytes {t1 - t0}ms · 总耗时 {t2}ms · 递归发现 {CountStats.FileCount} 个文件 / {CountStats.DirectoryCount} 个目录", cancellationToken).ConfigureAwait(false);
            return account;
        }
        finally
        {
            _transferGate.Release();
        }
    }

    public async IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        CloudObject[] snapshot;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localDirectory = ResolvePath(directory);
            if (!Directory.Exists(localDirectory))
            {
                snapshot = Array.Empty<CloudObject>();
            }
            else
            {
                EnsureNoReparsePoints(localDirectory);

                var discovered = new List<FileSystemEntry>();
                foreach (var path in Directory.EnumerateFileSystemEntries(localDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsInternalHidden(Path.GetFileName(path)))
                        discovered.Add(new FileSystemEntry(path, Directory.Exists(path)));
                }
                var entries = discovered
                    .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                    .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.Ordinal)
                    .ToArray();

                var objects = new List<CloudObject>(entries.Length);
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureNoReparsePoints(entry.Path);
                    var name = Path.GetFileName(entry.Path);
                    var childPath = directory.Value == "/"
                        ? new CloudPath("/" + name)
                        : new CloudPath(directory.Value.TrimEnd('/') + "/" + name);
                    var kind = entry.IsDirectory ? CloudObjectKind.Folder : GetObjectKind(name);
                    var size = entry.IsDirectory ? 0 : new FileInfo(entry.Path).Length;
                    var modified = entry.IsDirectory
                        ? new DirectoryInfo(entry.Path).LastWriteTimeUtc
                        : File.GetLastWriteTimeUtc(entry.Path);

                    objects.Add(new CloudObject(
                        Kind,
                        childPath.Value,
                        childPath,
                        name,
                        kind,
                        size,
                        new DateTimeOffset(modified, TimeSpan.Zero),
                        kind == CloudObjectKind.Image ? childPath.Value : null,
                        IsHanabeManaged(childPath)));
                }
                snapshot = objects.ToArray();
            }
        }
        finally
        {
            _transferGate.Release();
        }

        foreach (var item in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public Task<Stream?> OpenThumbnailAsync(CloudObject item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateItem(item);
        if (item.Kind != CloudObjectKind.Image)
            return Task.FromResult<Stream?>(null);
        return OpenFileAsync(item.Path, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(CloudObject item, CancellationToken cancellationToken)
    {
        ValidateItem(item);
        cancellationToken.ThrowIfCancellationRequested();
        var stream = await OpenFileAsync(item.Path, cancellationToken).ConfigureAwait(false);
        return stream ?? throw new FileNotFoundException("Cloud object is not a readable file.", item.Path.Value);
    }

    public async Task<CloudObject> EnsureFolderAsync(CloudPath path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localPath = ResolvePath(path);
            Directory.CreateDirectory(localPath);
            EnsureNoReparsePoints(localPath);
            return CreateFolderObject(path, localPath);
        }
        finally
        {
            _transferGate.Release();
        }
    }

    public async Task<string> UploadAsync(
        string localPath,
        CloudPath destination,
        IProgress<CloudUploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Local path is required.", nameof(localPath));
        if (!Path.IsPathFullyQualified(localPath))
            throw new ArgumentException("Local path must be absolute.", nameof(localPath));
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Value == "/")
            throw new ArgumentException("Upload destination must include a file name.", nameof(destination));
        if (Directory.Exists(localPath))
            throw new ArgumentException("Local upload source must be a file.", nameof(localPath));
        if (!File.Exists(localPath))
            throw new FileNotFoundException("Local upload source was not found.", localPath);

        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = ResolvePath(destination);
            var parent = Path.GetDirectoryName(target)
                ?? throw new InvalidOperationException("Upload destination has no parent directory.");
            Directory.CreateDirectory(parent);
            EnsureNoReparsePoints(parent);
            EnsureNoReparsePoints(target);

            var sourceInfo = new FileInfo(localPath);
            var size = sourceInfo.Length;
            var oldSize = File.Exists(target) ? new FileInfo(target).Length : 0L;
            var used = GetUsedBytes(cancellationToken);
            if (used - oldSize > _capacityBytes - size)
                throw new IOException("模拟云盘容量不足。");

            temporary = Path.Combine(parent, ".hanabe-upload-" + Guid.NewGuid().ToString("N") + ".tmp");
            progress?.Report(new CloudUploadProgress(0, size, localPath));
            EnsureNoReparsePoints(localPath, requireRemoteRoot: false);
            await using (var source = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                EnsureNoReparsePoints(localPath, requireRemoteRoot: false);
                var buffer = new byte[CopyBufferSize];
                long uploaded = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    uploaded += read;
                    if (uploaded > size)
                        throw new IOException("Local source changed while it was being uploaded.");
                    progress?.Report(new CloudUploadProgress(uploaded, size, localPath));
                }

                if (uploaded != size)
                    throw new IOException("Local source changed while it was being uploaded.");
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoReparsePoints(parent);
            EnsureNoReparsePoints(target);
            File.Move(temporary, target, true);
            temporary = null;
            _uploadSessions[destination.Value] = new UploadSession(destination, Path.GetFullPath(localPath));
            return destination.Value;
        }
        finally
        {
            if (temporary is not null)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
            _transferGate.Release();
        }
    }

    public async Task<CloudVerificationResult> VerifyAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken)
    {
        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await VerifyCoreAsync(remoteId, expected, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _transferGate.Release();
        }
    }

    private async Task<CloudVerificationResult> VerifyCoreAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteId))
            throw new ArgumentException("Remote id is required.", nameof(remoteId));
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();

        CloudPath remotePath;
        try { remotePath = new CloudPath(remoteId); }
        catch (ArgumentException ex) { throw new ArgumentException("Remote id is not a valid cloud path.", nameof(remoteId), ex); }
        var normalizedRemoteId = remotePath.Value;
        var suffix = "/" + expected.RelativePath.Value;
        var isKnownUpload = _uploadSessions.TryGetValue(normalizedRemoteId, out var uploadSession);
        var matchesRelativePath = isKnownUpload
            ? IsExpectedUploadSession(normalizedRemoteId, uploadSession!, expected)
            : normalizedRemoteId.Equals(suffix, _pathComparison);
        if (!matchesRelativePath)
            return new CloudVerificationResult(false, "远端路径与预期相对路径不匹配。", normalizedRemoteId);

        var local = ResolvePath(remotePath);
        EnsureNoReparsePoints(local);
        if (!File.Exists(local))
            return new CloudVerificationResult(false, "远端文件不存在。", normalizedRemoteId);
        var info = new FileInfo(local);
        if (info.Length != expected.Size)
            return new CloudVerificationResult(false, "文件大小不匹配。", normalizedRemoteId);
        if (!string.IsNullOrWhiteSpace(expected.ContentHash))
        {
            EnsureNoReparsePoints(local);
            await using var stream = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read,
                CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            EnsureNoReparsePoints(local);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!hash.Equals(expected.ContentHash, StringComparison.OrdinalIgnoreCase))
                return new CloudVerificationResult(false, "SHA-256 校验不匹配。", normalizedRemoteId);
        }

        return new CloudVerificationResult(true, "远端文件存在且校验通过。", normalizedRemoteId);
    }

    private async Task<Stream?> OpenFileAsync(CloudPath path, CancellationToken cancellationToken)
    {
        await _transferGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var localPath = ResolvePath(path);
            EnsureNoReparsePoints(localPath);
            if (!File.Exists(localPath))
                return null;
            cancellationToken.ThrowIfCancellationRequested();
            var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            try
            {
                EnsureNoReparsePoints(localPath);
                return await Task.FromResult<Stream>(stream).ConfigureAwait(false);
            }
            catch
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _transferGate.Release();
        }
    }

    private void ValidateItem(CloudObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Provider != Kind)
            throw new ArgumentException("Cloud object belongs to another provider.", nameof(item));
        if (!new CloudPath(item.RemoteId).Value.Equals(item.Path.Value, _pathComparison))
            throw new ArgumentException("Cloud object path and remote id do not match.", nameof(item));
    }

    private CloudObject CreateFolderObject(CloudPath path, string localPath)
    {
        var name = path.Value == "/" ? "/" : path.Value.Split('/').Last();
        var modified = Directory.GetLastWriteTimeUtc(localPath);
        return new CloudObject(Kind, path.Value, path, name, CloudObjectKind.Folder, 0,
            new DateTimeOffset(modified, TimeSpan.Zero), null, IsHanabeManaged(path));
    }

    private string ResolvePath(CloudPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var relative = path.Value.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        string candidate;
        try
        {
            candidate = relative.Length == 0
                ? _remoteRoot
                : Path.GetFullPath(Path.Combine(_remoteRoot, relative));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("Cloud path is not a valid local path.", nameof(path), ex);
        }

        if (!IsWithinRoot(candidate))
            throw new SecurityException("Cloud path escapes the configured remote root.");
        EnsureNoReparsePoints(candidate);
        return candidate;
    }

    private bool IsWithinRoot(string path)
    {
        var root = _remoteRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return candidate.Equals(root, _pathComparison) ||
            candidate.StartsWith(root + Path.DirectorySeparatorChar, _pathComparison);
    }

    private void EnsureNoReparsePoints(string path, bool requireRemoteRoot = true)
    {
        var current = Path.GetFullPath(path);
        while (true)
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new SecurityException("Reparse points are not allowed inside the simulated cloud root.");
            if (current.Equals(_remoteRoot, _pathComparison) ||
                (!requireRemoteRoot && current.Equals(Path.GetPathRoot(current), _pathComparison)))
                return;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || (requireRemoteRoot && !IsWithinRoot(parent)))
                throw new SecurityException("Cloud path has no safe parent inside the remote root.");
            current = parent;
        }
    }

    private long GetUsedBytes(CancellationToken cancellationToken)
    {
        long total = 0;
        var fileCount = 0;
        var directoryCount = 0;
        var pending = new Stack<string>(new[] { _remoteRoot });
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            directoryCount++;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureNoReparsePoints(entry);
                if (IsProviderUploadTemporary(Path.GetFileName(entry)))
                {
                    try
                    {
                        File.Delete(entry);
                        if (!File.Exists(entry))
                            continue;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (Directory.Exists(entry)) pending.Push(entry);
                else if (File.Exists(entry))
                {
                    fileCount++;
                    total = checked(total + new FileInfo(entry).Length);
                }
            }
        }

        CountStats.FileCount = fileCount;
        CountStats.DirectoryCount = directoryCount;
        return total;
    }

    private static class CountStats
    {
        public static int FileCount;
        public static int DirectoryCount;
    }

    private static CloudObjectKind GetObjectKind(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp") return CloudObjectKind.Image;
        if (extension is ".arw" or ".cr2" or ".cr3" or ".nef" or ".raf" or ".rw2" or ".orf" or ".dng" or ".raw") return CloudObjectKind.Raw;
        if (extension is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".wmv" or ".lrf") return CloudObjectKind.Video;
        if (extension is ".aac" or ".wav" or ".mp3" or ".m4a" or ".flac" or ".ogg") return CloudObjectKind.Audio;
        return CloudObjectKind.Other;
    }

    private static bool IsHanabeManaged(CloudPath path) =>
        path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("Hanabe照片备份", StringComparison.OrdinalIgnoreCase));

    private static bool IsInternalHidden(string? name) =>
        name is not null && name.Length > 0 && name[0] == '.' &&
        (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));

    private static bool IsProviderUploadTemporary(string? name)
    {
        const string prefix = ".hanabe-upload-";
        const string suffix = ".tmp";
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;
        var guidText = name[prefix.Length..^suffix.Length];
        return Guid.TryParseExact(guidText, "N", out _);
    }

    private bool IsExpectedUploadSession(
        string normalizedRemoteId,
        UploadSession session,
        CloudTransferFile expected)
    {
        // The provider contract does not carry the job destination. The scheduler must also compare
        // job.Destination.Combine(expected.RelativePath) with remoteId when one local source is sent
        // to multiple cloud roots; here we bind the result to this provider upload's source and target.
        var expectedSuffix = "/" + expected.RelativePath.Value;
        var expectedLocalPath = Path.GetFullPath(expected.LocalPath);
        return session.Destination.Value.Equals(normalizedRemoteId, _pathComparison) &&
            session.LocalSourcePath.Equals(expectedLocalPath, _pathComparison) &&
            (session.Destination.Value.Equals(expectedSuffix, _pathComparison) ||
             session.Destination.Value.EndsWith(expectedSuffix, _pathComparison));
    }

    private static async Task WriteDiagnosticsAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HanabePhotoManager",
                "Cloud");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "simulated-provider-diagnostics.log");
            var line = $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, line, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort diagnostics; never crash the provider for a log write failure.
        }
    }

    private readonly record struct FileSystemEntry(string Path, bool IsDirectory);

    private sealed record UploadSession(CloudPath Destination, string LocalSourcePath);
}
