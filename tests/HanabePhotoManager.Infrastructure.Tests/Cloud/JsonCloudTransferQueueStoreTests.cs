using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class JsonCloudTransferQueueStoreTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt =
        DateTimeOffset.Parse("2026-07-16T00:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabeCloudQueueTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsAcrossInstances()
    {
        var path = Path.Combine(_root, "nested", "queue.json");
        var pending = CreateJob(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CloudTransferState.Pending,
            @"C:\Camera\DCIM\100MSDCF\DSC0001.JPG",
            new CloudRelativePath(@"7月\07.14\JPG生图\JK0001.JPG"),
            uploadedBytes: 4,
            remoteId: "quark-pending-1");
        var completed = CreateCompletedJob();

        await new JsonCloudTransferQueueStore(path).SaveAsync([pending, completed]);
        var loaded = await new JsonCloudTransferQueueStore(path).LoadAsync();

        loaded.Should().BeEquivalentTo([pending, completed]);
        loaded[0].Files[0].LocalPath.Should().Be(@"C:\Camera\DCIM\100MSDCF\DSC0001.JPG");
        loaded[0].Files[0].RelativePath.Value.Should().Be("7月/07.14/JPG生图/JK0001.JPG");
        loaded[1].State.Should().Be(CloudTransferState.Completed);
        loaded[1].FileVerifications.Should().ContainSingle()
            .Which.Reason.Should().Be("大小和 SHA-256 已验证");
        Directory.Exists(Path.GetDirectoryName(path)).Should().BeTrue();
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyQueue()
    {
        var loaded = await new JsonCloudTransferQueueStore(Path.Combine(_root, "missing.json"))
            .LoadAsync();

        loaded.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"jobs\":[]}")]
    public async Task LoadAsync_WhenJsonIsMalformedOrNotAQueue_FailsExplicitly(string json)
    {
        var path = Path.Combine(_root, "queue.json");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(path, json);

        var act = () => new JsonCloudTransferQueueStore(path).LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*queue.json*");
    }

    [Fact]
    public async Task LoadAsync_WhenJsonViolatesCloudModelInvariants_FailsExplicitly()
    {
        var path = Path.Combine(_root, "queue.json");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            path,
            """
            [
              {
                "id": "22222222-2222-2222-2222-222222222222",
                "provider": 1,
                "destination": "/backup",
                "priority": 1,
                "state": 5,
                "files": [
                  {
                    "localPath": "C:\\source.jpg",
                    "relativePath": "source.jpg",
                    "size": 4,
                    "contentHash": null,
                    "uploadedBytes": 4,
                    "remoteId": "remote-1"
                  }
                ],
                "createdAt": "2026-07-16T00:00:00+00:00",
                "fileVerifications": []
              }
            ]
            """);

        var act = () => new JsonCloudTransferQueueStore(path).LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*queue.json*");
    }

    [Theory]
    [InlineData("job", "id")]
    [InlineData("job", "provider")]
    [InlineData("job", "destination")]
    [InlineData("job", "priority")]
    [InlineData("job", "state")]
    [InlineData("job", "files")]
    [InlineData("job", "createdAt")]
    [InlineData("job", "fileVerifications")]
    [InlineData("file", "localPath")]
    [InlineData("file", "relativePath")]
    [InlineData("file", "size")]
    [InlineData("file", "uploadedBytes")]
    [InlineData("file", "remoteId")]
    [InlineData("verification", "remoteId")]
    [InlineData("verification", "verifiedAt")]
    [InlineData("verification", "isVerified")]
    [InlineData("verification", "reason")]
    public async Task LoadAsync_WhenRequiredFieldIsMissing_FailsExplicitly(
        string objectName,
        string propertyName)
    {
        var path = Path.Combine(_root, "queue.json");
        var store = new JsonCloudTransferQueueStore(path);
        await store.SaveAsync([CreateZeroByteCompletedJob()]);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsArray();
        var job = document[0]!.AsObject();
        var target = objectName switch
        {
            "job" => job,
            "file" => job["files"]![0]!.AsObject(),
            "verification" => job["fileVerifications"]![0]!.AsObject(),
            _ => throw new ArgumentOutOfRangeException(nameof(objectName))
        };
        target.Remove(propertyName).Should().BeTrue();
        await File.WriteAllTextAsync(path, document.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        var act = () => store.LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*queue.json*");
    }

    [Fact]
    public async Task ConcurrentAccessAcrossInstances_IsSerializedAndLeavesValidQueue()
    {
        var path = Path.Combine(_root, "queue.json");
        var jobs = Enumerable.Range(1, 12)
            .Select(index => CreateJob(
                Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                CloudTransferState.Pending,
                $@"C:\Camera\DSC{index:0000}.JPG",
                new CloudRelativePath($"JK{index:0000}.JPG")))
            .ToArray();

        await Task.WhenAll(jobs.Select(job =>
            new JsonCloudTransferQueueStore(path).SaveAsync([job])));

        var loaded = await new JsonCloudTransferQueueStore(path).LoadAsync();
        loaded.Should().ContainSingle();
        jobs.Select(job => job.Id).Should().Contain(loaded[0].Id);
        File.Exists(path + ".tmp").Should().BeFalse();
        var json = await File.ReadAllTextAsync(path);
        var parse = () => JsonDocument.Parse(json);
        parse.Should().NotThrow();
    }

    [Fact]
    public async Task SaveAsync_WhenLegacyTemporaryPathExists_UsesUniqueTemporaryFile()
    {
        var path = Path.Combine(_root, "queue.json");
        Directory.CreateDirectory(path + ".tmp");
        var store = new JsonCloudTransferQueueStore(path);

        await store.SaveAsync([CreateJob()]);

        (await store.LoadAsync()).Should().ContainSingle();
        Directory.Exists(path + ".tmp").Should().BeTrue();
        Directory.GetFiles(_root, "queue.json.*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenLockIsHeldExternally_CanBeCanceledWithoutReplacingExistingQueue()
    {
        var path = Path.Combine(_root, "queue.json");
        var store = new JsonCloudTransferQueueStore(path);
        var original = CreateJob();
        await store.SaveAsync([original]);
        var originalJson = await File.ReadAllTextAsync(path);
        await using var externalLock = new FileStream(
            path + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var act = () => new JsonCloudTransferQueueStore(path).SaveAsync([
            CreateJob(Guid.Parse("33333333-3333-3333-3333-333333333333"))
        ], cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await File.ReadAllTextAsync(path)).Should().Be(originalJson);
    }

    [Fact]
    public async Task SaveAsync_WhenOnePathIsExternallyLocked_DifferentPathStillRuns()
    {
        var firstPath = Path.Combine(_root, "first", "queue.json");
        var secondPath = Path.Combine(_root, "second", "queue.json");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        await using var externalLock = new FileStream(
            firstPath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var cancellation = new CancellationTokenSource();
        var blockedSave = new JsonCloudTransferQueueStore(firstPath)
            .SaveAsync([CreateJob()], cancellation.Token);

        await Task.Delay(75);
        blockedSave.IsCompleted.Should().BeFalse();
        await new JsonCloudTransferQueueStore(secondPath)
            .SaveAsync([CreateJob()])
            .WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var act = async () => await blockedSave;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SaveAsync_WhenCanceled_DoesNotCreateQueueOrTemporaryFile()
    {
        var path = Path.Combine(_root, "queue.json");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new JsonCloudTransferQueueStore(path);

        var act = () => store.SaveAsync([CreateJob()], cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(path).Should().BeFalse();
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenAtomicReplaceFails_PreservesExistingTargetAndCleansTemporaryFile()
    {
        var path = Path.Combine(_root, "queue.json");
        Directory.CreateDirectory(path);
        var sentinel = Path.Combine(path, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        var store = new JsonCloudTransferQueueStore(path);

        var act = () => store.SaveAsync([CreateJob()]);

        await act.Should().ThrowAsync<Exception>()
            .Where(exception =>
                exception.GetType() == typeof(IOException) ||
                exception.GetType() == typeof(UnauthorizedAccessException));
        File.ReadAllText(sentinel).Should().Be("keep");
        Directory.GetFiles(_root, "queue.json.*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenCanceledAndTemporaryCleanupFails_ReleasesLockAndPreservesCancellation()
    {
        var path = Path.Combine(_root, "queue.json");
        var store = new JsonCloudTransferQueueStore(path);
        await store.SaveAsync([CreateJob()]);
        var originalJson = await File.ReadAllTextAsync(path);
        var longJob = CreateJobWithLargeHash();
        using var cancellation = new CancellationTokenSource();
        string? temporaryPath = null;

        try
        {
            var save = store.SaveAsync([longJob], cancellation.Token);
            temporaryPath = await WaitForTemporaryFileAsync(path, save);
            File.SetAttributes(temporaryPath, FileAttributes.ReadOnly);
            cancellation.Cancel();

            var act = async () => await save;

            var cancellationException = await act.Should().ThrowAsync<OperationCanceledException>();
            cancellationException.Which.Data["TemporaryCleanupException"]
                .Should().BeOfType<UnauthorizedAccessException>();
            (await File.ReadAllTextAsync(path)).Should().Be(originalJson);
            using var nextCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await new JsonCloudTransferQueueStore(path)
                .SaveAsync([CreateJob()], nextCancellation.Token);
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.SetAttributes(temporaryPath, FileAttributes.Normal);
                File.Delete(temporaryPath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_CompletedJobPreservesEvidenceForEveryFile()
    {
        var path = Path.Combine(_root, "queue.json");
        var files = new[]
        {
            new CloudTransferFile(
                @"D:\Camera\JK0001.JPG",
                new CloudRelativePath("JPG生图/JK0001.JPG"),
                3,
                "hash-1",
                3,
                "remote-1"),
            new CloudTransferFile(
                @"D:\Camera\JK0001.ARW",
                new CloudRelativePath("RAW生图/JK0001.ARW"),
                5,
                "hash-2",
                5,
                "remote-2")
        };
        var verifying = new CloudTransferJob(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Verifying,
            files,
            CreatedAt);
        var completed = verifying.MarkVerified(
            [
                new CloudVerificationResult(true, "verified jpg", "remote-1"),
                new CloudVerificationResult(true, "verified raw", "remote-2")
            ],
            CreatedAt.AddMinutes(1));

        await new JsonCloudTransferQueueStore(path).SaveAsync([completed]);
        var loaded = await new JsonCloudTransferQueueStore(path).LoadAsync();

        loaded.Should().ContainSingle();
        loaded[0].FileVerifications.Select(item => item.RemoteId)
            .Should().BeEquivalentTo("remote-1", "remote-2");
        loaded[0].IsVerified.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CloudTransferJob CreateJob(
        Guid? id = null,
        CloudTransferState state = CloudTransferState.Pending,
        string localPath = @"C:\Camera\DSC0001.JPG",
        CloudRelativePath? relativePath = null,
        long uploadedBytes = 0,
        string? remoteId = null)
    {
        var file = new CloudTransferFile(
            localPath,
            relativePath ?? new CloudRelativePath("JPG生图/JK0001.JPG"),
            10,
            "sha256:abc",
            uploadedBytes,
            remoteId);

        return new CloudTransferJob(
            id ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CloudProviderKind.Quark,
            new CloudPath("/Hanabe照片备份/7月/07.14"),
            CloudTransferPriority.Required,
            state,
            [file],
            CreatedAt);
    }

    private static CloudTransferJob CreateCompletedJob()
    {
        var file = new CloudTransferFile(
            @"D:\Sony\M4ROOT\CLIP\C0001.MP4",
            new CloudRelativePath("视频/JK0002.MP4"),
            20,
            "sha256:def",
            uploadedBytes: 20,
            remoteId: "quark-completed-1");
        var job = new CloudTransferJob(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CloudProviderKind.Quark,
            new CloudPath("/Hanabe照片备份/7月/07.14"),
            CloudTransferPriority.Required,
            CloudTransferState.Verifying,
            [file],
            CreatedAt);

        return job.MarkVerified(
            [new CloudVerificationResult(true, "大小和 SHA-256 已验证", "quark-completed-1")],
            CreatedAt.AddMinutes(5));
    }

    private static CloudTransferJob CreateZeroByteCompletedJob()
    {
        var file = new CloudTransferFile(
            @"C:\Camera\EMPTY.JPG",
            new CloudRelativePath("JPG生图/EMPTY.JPG"),
            size: 0,
            contentHash: null,
            uploadedBytes: 0,
            remoteId: "remote-empty");
        var verifying = new CloudTransferJob(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Verifying,
            [file],
            DateTimeOffset.MinValue);

        return verifying.MarkVerified(
            [new CloudVerificationResult(true, "verified empty file", "remote-empty")],
            DateTimeOffset.MinValue);
    }

    private static CloudTransferJob CreateJobWithLargeHash()
    {
        var file = new CloudTransferFile(
            @"C:\Camera\LARGE.JPG",
            new CloudRelativePath("JPG生图/LARGE.JPG"),
            1,
            new string('a', 32 * 1024 * 1024));
        return new CloudTransferJob(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CloudProviderKind.Quark,
            new CloudPath("/backup"),
            CloudTransferPriority.Required,
            CloudTransferState.Pending,
            [file],
            CreatedAt);
    }

    private static async Task<string> WaitForTemporaryFileAsync(string path, Task save)
    {
        var directory = Path.GetDirectoryName(path)!;
        var pattern = Path.GetFileName(path) + "*.tmp";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            var temporaryPath = Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern).SingleOrDefault()
                : null;
            if (temporaryPath is not null)
            {
                return temporaryPath;
            }

            if (save.IsCompleted)
            {
                throw new InvalidOperationException("Save completed before a temporary file could be observed.");
            }

            await Task.Delay(5, timeout.Token);
        }

        throw new TimeoutException("A queue temporary file was not created in time.");
    }
}
