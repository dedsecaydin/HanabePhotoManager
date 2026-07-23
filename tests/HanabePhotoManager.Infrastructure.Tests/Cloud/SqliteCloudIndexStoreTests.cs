using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;
using Microsoft.Data.Sqlite;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class SqliteCloudIndexStoreTests : IDisposable
{
    private static readonly DateTimeOffset ModifiedAt =
        DateTimeOffset.Parse("2026-07-16T08:09:10.1234567+08:00");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabeCloudIndexTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task QueryChildren_IsolatesProviderAndExactParent()
    {
        var store = Store();
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "quark-a", "/photos/a.jpg"),
            Object(CloudProviderKind.Quark, "quark-b", "/photos/nested/b.jpg"),
            Object(CloudProviderKind.Quark, "quark-c", "/other/c.jpg"),
            Object(CloudProviderKind.Baidu, "baidu-d", "/photos/d.jpg")]);

        var items = await store.QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath("/photos"));

        items.Select(item => item.Name).Should().Equal("a.jpg");
    }

    [Theory]
    [InlineData("/root.jpg", "/")]
    [InlineData("/照片/七月/JK0001.JPG", "/照片/七月")]
    [InlineData(@"\照片\七月\JK0002.JPG", "/照片/七月")]
    public async Task UpsertAsync_ComputesParentWithCloudPathSemantics(
        string itemPath,
        string expectedParent)
    {
        var store = Store();
        var item = Object(CloudProviderKind.Quark, itemPath, itemPath);
        await store.UpsertAsync([item]);

        var children = await store.QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath(expectedParent));

        children.Should().ContainSingle().Which.Should().BeEquivalentTo(item);
    }

    [Fact]
    public async Task SaveAndQuery_RoundTripsEveryFieldAcrossInstances()
    {
        var databasePath = DatabasePath();
        var items = new[]
        {
            Object(CloudProviderKind.Quark, "folder", "/Hanabe照片备份/七月", CloudObjectKind.Folder,
                size: 0, thumbnailKey: null, isHanabeManaged: true),
            Object(CloudProviderKind.Quark, "图片-'参数", "/Hanabe照片备份/图片-'参数.JPG",
                CloudObjectKind.Image, thumbnailKey: "缩略图/键", isHanabeManaged: true),
            Object(CloudProviderKind.Quark, "raw", "/Hanabe照片备份/raw.ARW", CloudObjectKind.Raw),
            Object(CloudProviderKind.Quark, "video", "/Hanabe照片备份/video.MP4", CloudObjectKind.Video),
            Object(CloudProviderKind.Quark, "audio", "/Hanabe照片备份/audio.AAC", CloudObjectKind.Audio),
            Object(CloudProviderKind.Quark, "other", "/Hanabe照片备份/data.XML", CloudObjectKind.Other)
        };

        await new SqliteCloudIndexStore(databasePath).UpsertAsync(items);
        var loaded = await new SqliteCloudIndexStore(databasePath).QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath("/Hanabe照片备份"));

        loaded.Should().BeEquivalentTo(items);
        loaded.Should().OnlyContain(item => item.ModifiedAt.Equals(ModifiedAt));
        Directory.Exists(Path.GetDirectoryName(databasePath)).Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_ForExistingProviderAndRemoteId_UpdatesEveryMutableField()
    {
        var store = Store();
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "same", "/old/old.jpg", CloudObjectKind.Image,
                size: 4, thumbnailKey: "old-thumb")]);
        var updated = new CloudObject(
            CloudProviderKind.Quark,
            "same",
            new CloudPath("/new/新名称.MP4"),
            "新名称.MP4",
            CloudObjectKind.Video,
            99,
            ModifiedAt.AddDays(1),
            null,
            true);

        await store.UpsertAsync([updated]);

        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/old")))
            .Should().BeEmpty();
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/new")))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task QueryChildren_ReturnsFoldersFirstThenOrdinalIgnoreCaseNamesWithStableTieBreaks()
    {
        var store = Store();
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "file-z", "/photos/z.jpg"),
            Object(CloudProviderKind.Quark, "file-lower", "/photos/a.jpg"),
            Object(CloudProviderKind.Quark, "folder-b", "/photos/B", CloudObjectKind.Folder),
            Object(CloudProviderKind.Quark, "file-upper-2", "/photos/A.jpg"),
            Object(CloudProviderKind.Quark, "file-upper-1", "/photos/A.jpg")]);

        var first = await store.QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath("/photos"));
        var second = await Store().QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath("/photos"));

        first.Select(item => item.RemoteId).Should().Equal(
            "folder-b", "file-upper-1", "file-upper-2", "file-lower", "file-z");
        second.Select(item => item.RemoteId).Should().Equal(first.Select(item => item.RemoteId));
    }

    [Fact]
    public async Task RemoveProviderAsync_DeletesOnlyRequestedProvider()
    {
        var store = Store();
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "same-id", "/photos/quark.jpg"),
            Object(CloudProviderKind.Baidu, "same-id", "/photos/baidu.jpg")]);

        await store.RemoveProviderAsync(CloudProviderKind.Quark);

        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().BeEmpty();
        (await store.QueryChildrenAsync(CloudProviderKind.Baidu, new CloudPath("/photos")))
            .Should().ContainSingle().Which.Name.Should().Be("baidu.jpg");
    }

    [Fact]
    public async Task ConcurrentFirstUseAcrossInstances_InitializesSchemaAndPreservesAllRows()
    {
        var databasePath = DatabasePath();
        var items = Enumerable.Range(1, 20)
            .Select(index => Object(
                CloudProviderKind.Simulated,
                $"remote-{index:00}",
                $"/photos/{index:00}.jpg"))
            .ToArray();

        await Task.WhenAll(items.Select(item =>
            new SqliteCloudIndexStore(databasePath).UpsertAsync([item])));

        var loaded = await new SqliteCloudIndexStore(databasePath).QueryChildrenAsync(
            CloudProviderKind.Simulated,
            new CloudPath("/photos"));
        loaded.Should().HaveCount(items.Length);

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_cloud_children';";
        Convert.ToInt64(await command.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertAsync_WhenCanceledBeforeTransaction_LeavesExistingRowsUntouched()
    {
        var store = Store();
        var original = Object(CloudProviderKind.Quark, "existing", "/photos/existing.jpg");
        await store.UpsertAsync([original]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => store.UpsertAsync([
            Object(CloudProviderKind.Quark, "new-1", "/photos/new-1.jpg"),
            Object(CloudProviderKind.Quark, "new-2", "/photos/new-2.jpg")
        ], cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task UpsertAsync_WhenCanceledAfterFirstStatement_RollsBackWholeBatch()
    {
        var databasePath = DatabasePath();
        var store = Store();
        var original = Object(CloudProviderKind.Quark, "existing", "/photos/existing.jpg");
        await store.UpsertAsync([original]);
        using var cancellation = new CancellationTokenSource();

        var act = () => store.UpsertAsync(
            CancelAfterFirstStatement(databasePath, cancellation),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task UpsertAsync_WhenExternalWriterHoldsDatabase_CancelsQuicklyWithoutBlockingCaller()
    {
        var databasePath = DatabasePath();
        var store = Store();
        var original = Object(CloudProviderKind.Quark, "existing", "/photos/existing.jpg");
        await store.UpsertAsync([original]);
        using var lockConnection = OpenConnection(databasePath);
        using var lockTransaction = lockConnection.BeginTransaction(deferred: false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var stopwatch = Stopwatch.StartNew();
        var completionStopwatch = Stopwatch.StartNew();
        var pending = store.UpsertAsync([
            Object(CloudProviderKind.Quark, "blocked", "/photos/blocked.jpg")
        ], cancellation.Token);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        var act = async () => await pending.WaitAsync(TimeSpan.FromSeconds(2));
        await act.Should().ThrowAsync<OperationCanceledException>();
        completionStopwatch.Stop();
        completionStopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(750));
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task RemoveProviderAsync_WhenExternalWriterHoldsDatabase_CancelsAndPreservesProvider()
    {
        var databasePath = DatabasePath();
        var store = Store();
        var original = Object(CloudProviderKind.Quark, "existing", "/photos/existing.jpg");
        await store.UpsertAsync([original]);
        using var lockConnection = OpenConnection(databasePath);
        using var lockTransaction = lockConnection.BeginTransaction(deferred: false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var stopwatch = Stopwatch.StartNew();
        var completionStopwatch = Stopwatch.StartNew();
        var pending = store.RemoveProviderAsync(CloudProviderKind.Quark, cancellation.Token);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        var act = async () => await pending.WaitAsync(TimeSpan.FromSeconds(2));
        await act.Should().ThrowAsync<OperationCanceledException>();
        completionStopwatch.Stop();
        completionStopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(750));
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task OperationsOnSameStore_RunInInvocationOrder()
    {
        var store = Store();
        var save = store.UpsertAsync([
            Object(CloudProviderKind.Quark, "queued", "/photos/queued.jpg")
        ]);
        var remove = store.RemoveProviderAsync(CloudProviderKind.Quark);

        await Task.WhenAll(save, remove);

        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task QueuedCancellation_CompletesImmediatelyButKeepsFollowingOperationBehindPredecessor()
    {
        var databasePath = DatabasePath();
        var store = Store();
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "seed", "/photos/seed.jpg")
        ]);
        using var lockConnection = OpenConnection(databasePath);
        var lockTransaction = lockConnection.BeginTransaction(deferred: false);
        Task first = Task.CompletedTask;
        Task canceled = Task.CompletedTask;
        Task third = Task.CompletedTask;
        Exception? cancellationFailure = null;
        using var cancellation = new CancellationTokenSource();
        try
        {
            first = store.UpsertAsync([
                Object(CloudProviderKind.Quark, "first", "/photos/first.jpg")
            ]);
            canceled = store.UpsertAsync([
                Object(CloudProviderKind.Quark, "canceled", "/photos/canceled.jpg")
            ], cancellation.Token);
            third = store.RemoveProviderAsync(CloudProviderKind.Quark);

            cancellation.Cancel();
            cancellationFailure = await Record.ExceptionAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                var canceledAct = async () => await canceled.WaitAsync(TimeSpan.FromMilliseconds(500));
                await canceledAct.Should().ThrowAsync<OperationCanceledException>();
                stopwatch.Stop();

                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(250));
                first.IsCompleted.Should().BeFalse();
                third.IsCompleted.Should().BeFalse();
            });
        }
        finally
        {
            lockTransaction.Dispose();
        }

        await first.WaitAsync(TimeSpan.FromSeconds(3));
        await third.WaitAsync(TimeSpan.FromSeconds(3));
        cancellationFailure.Should().BeNull();
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CompletedLastOperation_DoesNotRetainItsInputBatch()
    {
        var store = Store();
        var batchReference = await SaveCollectibleBatchAsync(store);

        ForceFullCollection();

        batchReference.IsAlive.Should().BeFalse();
        GetOperationTail(store).Should().BeSameAs(Task.CompletedTask);
        await store.UpsertAsync([
            Object(CloudProviderKind.Quark, "after-gc", "/photos/after-gc.jpg")
        ]);
        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/photos")))
            .Should().Contain(item => item.RemoteId == "after-gc");
    }

    [Fact]
    public async Task UpsertAsync_WithEmptySequence_IsANoOp()
    {
        var store = Store();

        await store.UpsertAsync([]);

        (await store.QueryChildrenAsync(CloudProviderKind.Quark, new CloudPath("/")))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_WithNullSequenceOrItem_RejectsInput()
    {
        var store = Store();

        var nullSequence = () => store.UpsertAsync(null!);
        var nullItem = () => store.UpsertAsync(new CloudObject[] { null! });

        await nullSequence.Should().ThrowAsync<ArgumentNullException>();
        await nullItem.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task ProviderOperations_WithUndefinedProvider_RejectInput(int value)
    {
        var store = Store();
        var provider = (CloudProviderKind)value;

        var query = () => store.QueryChildrenAsync(provider, new CloudPath("/"));
        var remove = () => store.RemoveProviderAsync(provider);

        await query.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await remove.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task QueryChildrenAsync_WhenDatabaseIsCorrupt_FailsExplicitly()
    {
        var databasePath = DatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await File.WriteAllBytesAsync(databasePath, "not a sqlite database"u8.ToArray());

        var act = () => new SqliteCloudIndexStore(databasePath).QueryChildrenAsync(
            CloudProviderKind.Quark,
            new CloudPath("/"));

        await act.Should().ThrowAsync<SqliteException>();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SqliteCloudIndexStore Store() => new(DatabasePath());

    private string DatabasePath() => Path.Combine(_root, "nested", "cloud.db");

    private static IEnumerable<CloudObject> CancelAfterFirstStatement(
        string databasePath,
        CancellationTokenSource cancellation)
    {
        yield return Object(CloudProviderKind.Quark, "new-1", "/photos/new-1.jpg");

        using var probeConnection = OpenConnection(databasePath);
        try
        {
            using var unexpectedTransaction = probeConnection.BeginTransaction(deferred: false);
            throw new InvalidOperationException(
                "The input sequence was enumerated before the store acquired its write transaction.");
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            cancellation.Cancel();
        }

        yield return Object(CloudProviderKind.Quark, "new-2", "/photos/new-2.jpg");
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False;Default Timeout=1");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 1;";
        command.ExecuteNonQuery();
        return connection;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> SaveCollectibleBatchAsync(
        SqliteCloudIndexStore store)
    {
        var batch = Enumerable.Range(1, 256)
            .Select(index => Object(
                CloudProviderKind.Quark,
                $"collectible-{index:000}",
                $"/photos/collectible-{index:000}.jpg"))
            .ToArray();
        var reference = new WeakReference(batch);

        await store.UpsertAsync(batch);

        return reference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceFullCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private static Task GetOperationTail(SqliteCloudIndexStore store) =>
        (Task)typeof(SqliteCloudIndexStore)
            .GetField("_operationTail", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;

    private static CloudObject Object(
        CloudProviderKind provider,
        string remoteId,
        string path,
        CloudObjectKind kind = CloudObjectKind.Image,
        long size = 4,
        string? thumbnailKey = null,
        bool isHanabeManaged = false)
    {
        var cloudPath = new CloudPath(path);
        var separator = cloudPath.Value.LastIndexOf('/');
        var name = cloudPath.Value[(separator + 1)..];
        return new CloudObject(
            provider,
            remoteId,
            cloudPath,
            name,
            kind,
            size,
            ModifiedAt,
            thumbnailKey,
            isHanabeManaged);
    }
}
