using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using HanabePhotoManager.App.Cloud;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.App.Tests.Cloud;

internal static class CloudViewModelTestData
{
    public static CloudHubTestContext Create(SynchronizationContext? synchronizationContext = null)
    {
        var rootItems = new[]
        {
            Item(CloudProviderKind.Simulated, "folder", "/photos", "photos", CloudObjectKind.Folder),
            Item(CloudProviderKind.Simulated, "readme", "/readme.jpg", "readme.jpg", CloudObjectKind.Image)
        };
        var photoItems = new[]
        {
            Item(CloudProviderKind.Simulated, "a", "/photos/a.jpg", "a.jpg", CloudObjectKind.Image)
        };
        var provider = new StubCloudProvider(new Dictionary<string, IReadOnlyList<CloudObject>>
        {
            ["/"] = rootItems,
            ["/photos"] = photoItems
        });
        var index = new MemoryIndex();
        var cache = new MemoryCache();
        return new CloudHubTestContext(
            new CloudHubViewModel(
                provider,
                index,
                cache,
                synchronizationContext ?? new TrackingSynchronizationContext()),
            provider,
            index,
            cache);
    }

    public static CloudObject Item(
        CloudProviderKind provider,
        string id,
        string path,
        string name,
        CloudObjectKind kind) =>
        new(provider, id, new CloudPath(path), name, kind, 4,
            DateTimeOffset.Parse("2026-07-16T00:00:00Z"), null, false);
}

internal sealed record CloudHubTestContext(
    CloudHubViewModel ViewModel,
    StubCloudProvider Provider,
    MemoryIndex Index,
    MemoryCache Cache) : IDisposable
{
    public void Dispose()
    {
        ViewModel.Dispose();
        Cache.Dispose();
    }
}

internal sealed class StubCloudProvider(
    IReadOnlyDictionary<string, IReadOnlyList<CloudObject>> directories) : ICloudProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CloudObject>> _directories = directories;

    public CloudProviderKind Kind { get; set; } = CloudProviderKind.Simulated;

    public CloudAccountState AccountState { get; set; } = new(
        CloudProviderKind.Simulated, true, "模拟网盘", 4, 1024, "已连接");

    public Exception? AccountException { get; set; }

    public Exception? ListException { get; set; }

    public Func<CloudPath, CancellationToken, Task>? BeforeListAsync { get; set; }

    public Func<CloudPath, CloudObject, int, CancellationToken, Task>? BeforeYieldAsync { get; set; }

    public bool HonorListCancellation { get; set; } = true;

    public Func<CloudObject, CancellationToken, Task<Stream?>> ThumbnailFactory { get; set; } =
        static (_, _) => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));

    private int _thumbnailOpenCount;
    private int _accountStateRequestCount;

    public int ThumbnailOpenCount => Volatile.Read(ref _thumbnailOpenCount);

    public int AccountStateRequestCount => Volatile.Read(ref _accountStateRequestCount);

    public ConcurrentQueue<string> ListedPaths { get; } = [];

    public Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _accountStateRequestCount);
        cancellationToken.ThrowIfCancellationRequested();
        return AccountException is null
            ? Task.FromResult(AccountState)
            : Task.FromException<CloudAccountState>(AccountException);
    }

    public async IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ListedPaths.Enqueue(directory.Value);
        if (BeforeListAsync is not null)
        {
            await BeforeListAsync(directory, cancellationToken);
        }

        if (ListException is not null)
        {
            throw ListException;
        }

        var index = 0;
        foreach (var item in _directories.GetValueOrDefault(directory.Value) ?? [])
        {
            if (HonorListCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (BeforeYieldAsync is not null)
            {
                await BeforeYieldAsync(
                    directory,
                    item,
                    index,
                    HonorListCancellation ? cancellationToken : CancellationToken.None);
            }

            yield return item;
            await Task.Yield();
            index++;
        }
    }

    public Task<Stream?> OpenThumbnailAsync(CloudObject item, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _thumbnailOpenCount);
        return ThumbnailFactory(item, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(CloudObject item, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));

    public Task<CloudObject> EnsureFolderAsync(CloudPath path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<string> UploadAsync(
        string localPath,
        CloudPath destination,
        IProgress<CloudUploadProgress>? progress,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<CloudVerificationResult> VerifyAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class MemoryIndex : ICloudIndexStore
{
    public ConcurrentQueue<IReadOnlyList<CloudObject>> UpsertBatches { get; } = [];

    public Exception? UpsertException { get; set; }

    public Task UpsertAsync(
        IEnumerable<CloudObject> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UpsertException is not null)
        {
            return Task.FromException(UpsertException);
        }

        UpsertBatches.Enqueue(items.ToArray());
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CloudObject>> QueryChildrenAsync(
        CloudProviderKind provider,
        CloudPath directory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CloudObject>>([]);

    public Task RemoveProviderAsync(
        CloudProviderKind provider,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class MemoryCache : ICloudCacheStore, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabePhotoManager.Tests",
        $"cloud-cache-{Guid.NewGuid():N}");
    private int _disposed;

    public ConcurrentQueue<string> RequestedKeys { get; } = [];

    public ConcurrentQueue<(string Key, bool Pinned)> PutRequests { get; } = [];

    public void Seed(string key, string path) => _paths[key] = path;

    public Task<string?> TryGetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestedKeys.Enqueue(key);
        return Task.FromResult(_paths.GetValueOrDefault(key));
    }

    public async Task<string> PutAsync(
        string key,
        Stream content,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        PutRequests.Enqueue((key, pinned));
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, Path.GetRandomFileName());
        await using (var destination = new FileStream(
                         path,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.Read))
        {
            await content.CopyToAsync(destination, cancellationToken);
        }

        _paths[key] = path;
        return path;
    }

    public Task TrimAsync(long maximumBytes, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _paths.Clear();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

internal sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
{
    public bool WasDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        WasDisposed = true;
        await base.DisposeAsync();
    }
}

internal sealed class TrackingSynchronizationContext : SynchronizationContext
{
    public int PostCount { get; private set; }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        PostCount++;
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            callback(state);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}

internal sealed class ThrowingSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) =>
        throw new InvalidOperationException("dispatcher unavailable");
}

internal sealed class DedicatedThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private int _disposed;

    public DedicatedThreadSynchronizationContext()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Hanabe cloud VM test UI"
        };
        _thread.Start();
        _started.Wait(TimeSpan.FromSeconds(5));
    }

    public int ThreadId => _thread.ManagedThreadId;

    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _queue.Add((callback, state));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _started.Dispose();
        _queue.Dispose();
    }

    private void Run()
    {
        SetSynchronizationContext(this);
        _started.Set();
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}
