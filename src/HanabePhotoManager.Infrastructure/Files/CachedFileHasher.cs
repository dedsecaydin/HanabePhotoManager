using System.Collections.Concurrent;
using System.Text.Json;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>查重专用持久指纹缓存；传输校验和删除前校验仍使用实时哈希。</summary>
public sealed class CachedFileHasher : IFileHasher
{
    private readonly IFileHasher _inner;
    private readonly string _path;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();
    private readonly object _writeGate = new();
    private long _hits;
    private long _computed;
    public long Hits => Interlocked.Read(ref _hits);
    public long Computed => Interlocked.Read(ref _computed);
    public bool Enabled { get; set; } = true;

    public void Clear()
    {
        lock (_writeGate)
        {
            if (File.Exists(_path)) File.Delete(_path);
            _entries.Clear();
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _computed, 0);
        }
    }

    public CachedFileHasher(IFileHasher inner, string path)
    {
        _inner = inner;
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<Entry>(line);
                    if (entry is not null && !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
                        _entries[entry.Key] = entry;
                }
                catch (JsonException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken) =>
        GetOrComputeAsync(path, "sha256-v1", token => _inner.ComputeSha256Async(path, token), cancellationToken);

    public async Task<string> GetOrComputeAsync(string path, string algorithm,
        Func<CancellationToken, Task<string>> compute, CancellationToken cancellationToken)
    {
        if (!Enabled) return await compute(cancellationToken).ConfigureAwait(false);
        var key = algorithm + "|" + Path.GetFullPath(path);
        var gate = _gates[(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(key) % (uint)_gates.Length];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stamp = ReadStamp(path);
            if (_entries.TryGetValue(key, out var cached) && cached.Stamp == stamp)
            {
                Interlocked.Increment(ref _hits);
                return cached.Value;
            }
            var value = await compute(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadStamp(path) != stamp) throw new IOException("计算指纹期间文件发生变化，请重试。");
            Save(new Entry(key, stamp, value));
            Interlocked.Increment(ref _computed);
            return value;
        }
        finally { gate.Release(); }
    }

    /// <summary>仅调用于刚完成实时验证、尚未修改的目标文件。</summary>
    public void RememberVerified(string path, string sha256) => Save(new Entry("sha256-v1|" + Path.GetFullPath(path), ReadStamp(path), sha256));

    private void Save(Entry entry)
    {
        lock (_writeGate)
        {
            _entries[entry.Key] = entry;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
                // 前置换行隔离异常退出留下的不完整尾行。
                var bytes = System.Text.Encoding.UTF8.GetBytes("\n" + JsonSerializer.Serialize(entry));
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static Stamp ReadStamp(string path)
    {
        var info = new FileInfo(path);
        return new Stamp(info.Length, info.LastWriteTimeUtc.Ticks, info.CreationTimeUtc.Ticks);
    }

    public sealed record Stamp(long Length, long ModifiedTicks, long CreatedTicks);
    public sealed record Entry(string Key, Stamp Stamp, string Value);
}
