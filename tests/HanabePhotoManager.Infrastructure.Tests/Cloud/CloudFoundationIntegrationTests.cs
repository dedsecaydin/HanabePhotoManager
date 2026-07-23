using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;
using Microsoft.Data.Sqlite;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class CloudFoundationIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "hanabe-cloud-foundation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public Task Restart_RestoresIndexQueueCache_AndKeepsCloudBrowseInteractive() =>
        ExecuteRestartAsync().WaitAsync(TimeSpan.FromSeconds(10));

    private async Task ExecuteRestartAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var databasePath = Path.Combine(_root, "state", "cloud.db");
        var queuePath = Path.Combine(_root, "state", "transfer-queue.json");
        var cacheRoot = Path.Combine(_root, "cache");
        var remoteRoot = Path.Combine(_root, "remote");
        var sourcePath = Path.Combine(_root, "source.jpg");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);

        var objects = CreateObjects();
        var pendingJob = CreatePendingJob(sourcePath);

        // First application lifetime: persist all durable state and write one nested cloud file.
        var index = new SqliteCloudIndexStore(databasePath);
        await index.UpsertAsync(objects);
        var queue = new JsonCloudTransferQueueStore(queuePath);
        await queue.SaveAsync([pendingJob]);

        var cache = new FileCloudCacheStore(cacheRoot, () => DateTimeOffset.UtcNow);
        for (var i = 1; i <= 5; i++)
        {
            using var content = new MemoryStream([(byte)i]);
            await cache.PutAsync($"thumb-{i}", content, pinned: i == 1);
        }

        var provider = new SimulatedCloudProvider(remoteRoot, 10_000_000);
        var remoteId = await provider.UploadAsync(
            sourcePath,
            new CloudPath("/Hanabe照片备份/2026/7月/07.16/JPG生图/source.jpg"),
            progress: null,
            CancellationToken.None);

        // A canceled transfer must return control to the caller without deleting the source.
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var canceledUpload = () => provider.UploadAsync(
            sourcePath,
            new CloudPath("/Hanabe照片备份/2026/7月/07.16/JPG生图/canceled.jpg"),
            progress: null,
            canceled.Token);
        await canceledUpload.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(sourcePath).Should().BeTrue();

        // A second lifetime must be able to reconstruct every durable store from disk.
        var reloadedIndex = new SqliteCloudIndexStore(databasePath);
        var reloadedObjects = await LoadAllObjectsAsync(reloadedIndex, objects);
        reloadedObjects.Should().HaveCount(objects.Count);
        reloadedObjects.Should().BeEquivalentTo(objects);

        var reloadedJobs = await new JsonCloudTransferQueueStore(queuePath).LoadAsync();
        reloadedJobs.Should().ContainSingle(job =>
            job.Id == pendingJob.Id &&
            job.State == CloudTransferState.Pending &&
            job.Files.Single().RelativePath.Value.Contains("JPG生图", StringComparison.Ordinal));

        var reloadedCache = new FileCloudCacheStore(cacheRoot, () => DateTimeOffset.UtcNow);
        for (var i = 1; i <= 5; i++)
        {
            var path = await reloadedCache.TryGetAsync($"thumb-{i}");
            path.Should().NotBeNullOrWhiteSpace();
            var bytes = await File.ReadAllBytesAsync(path!);
            bytes.Should().Equal((byte)i);
            new FileInfo(path!).Length.Should().Be(1);
        }

        // Restart must restore not only cache paths, but the persisted metadata for
        // every entry, including both pinned and non-pinned entries.
        using (var cacheIndex = JsonDocument.Parse(await File.ReadAllTextAsync(
                   Path.Combine(cacheRoot, "cache-index.json"))))
        {
            var entries = cacheIndex.RootElement.EnumerateArray().ToArray();
            entries.Should().HaveCount(5);
            for (var i = 1; i <= 5; i++)
            {
                var entry = entries.Single(item =>
                    item.GetProperty("key").GetString() == $"thumb-{i}");
                entry.GetProperty("relativePath").GetString().Should().NotBeNullOrWhiteSpace();
                entry.GetProperty("size").GetInt64().Should().Be(1);
                entry.GetProperty("pinned").GetBoolean().Should().Be(i == 1);
                entry.GetProperty("lastAccessedAt").GetDateTimeOffset().Should().NotBe(default);
            }
        }

        // Cache cleanup only removes local cache entries; it cannot touch the source file.
        await reloadedCache.TrimAsync(1);
        (await reloadedCache.TryGetAsync("thumb-1")).Should().NotBeNull();
        (await reloadedCache.TryGetAsync("thumb-2")).Should().BeNull();
        File.Exists(sourcePath).Should().BeTrue();

        // Recreating the simulator preserves nested browsing and the uploaded object.
        var restartedProvider = new SimulatedCloudProvider(remoteRoot, 10_000_000);
        var rootItems = await CollectAsync(restartedProvider.ListAsync(new CloudPath("/"), default));
        rootItems.Should().ContainSingle(item => item.Name == "Hanabe照片备份" && item.Kind == CloudObjectKind.Folder);
        var nestedItems = await CollectAsync(restartedProvider.ListAsync(
            new CloudPath("/Hanabe照片备份/2026/7月/07.16/JPG生图"),
            default));
        nestedItems.Should().ContainSingle(item => item.RemoteId == remoteId && item.Name == "source.jpg");

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    private static List<CloudObject> CreateObjects()
    {
        var objects = new List<CloudObject>(capacity: 2_000);
        var modifiedAt = DateTimeOffset.Parse("2026-07-16T08:09:10.1234567+08:00");
        for (var day = 1; day <= 20; day++)
        {
            var parent = $"/Hanabe照片备份/2026/7月/07.{day:00}/JPG生图";
            for (var number = 1; number <= 100; number++)
            {
                var name = $"夏日_{day:00}_{number:000}.JPG";
                var path = new CloudPath($"{parent}/{name}");
                objects.Add(new CloudObject(
                    CloudProviderKind.Simulated,
                    $"sim:{day:00}:{number:000}",
                    path,
                    name,
                    CloudObjectKind.Image,
                    size: 1024 + number,
                    modifiedAt,
                    thumbnailKey: $"thumb-{day:00}-{number:000}",
                    isHanabeManaged: true));
            }
        }

        return objects;
    }

    private static CloudTransferJob CreatePendingJob(string sourcePath) =>
        new(
            Guid.NewGuid(),
            CloudProviderKind.Quark,
            new CloudPath("/Hanabe照片备份/2026/7月/07.16"),
            CloudTransferPriority.Required,
            CloudTransferState.Pending,
            [new CloudTransferFile(
                sourcePath,
                new CloudRelativePath("7月/07.16/JPG生图/source.jpg"),
                size: 4,
                contentHash: null)],
            DateTimeOffset.Parse("2026-07-16T00:00:00Z"));

    private static async Task<IReadOnlyList<CloudObject>> LoadAllObjectsAsync(
        ICloudIndexStore index,
        IReadOnlyCollection<CloudObject> expected)
    {
        var result = new List<CloudObject>(expected.Count);
        foreach (var parent in expected.Select(item => GetParent(item.Path)).Distinct(StringComparer.Ordinal))
        {
            result.AddRange(await index.QueryChildrenAsync(
                CloudProviderKind.Simulated,
                new CloudPath(parent)));
        }

        return result;
    }

    private static string GetParent(CloudPath path)
    {
        var separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? "/" : path.Value[..separator];
    }

    private static async Task<IReadOnlyList<CloudObject>> CollectAsync(
        IAsyncEnumerable<CloudObject> source)
    {
        var items = new List<CloudObject>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!Directory.Exists(_root))
        {
            return;
        }

        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
                lastFailure = null;
                break;
            }
            catch (IOException exception)
            {
                lastFailure = exception;
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(50);
            }
        }

        if (Directory.Exists(_root))
        {
            throw new IOException(
                $"Failed to clean temporary cloud foundation root '{_root}' after retries.",
                lastFailure);
        }
    }
}
