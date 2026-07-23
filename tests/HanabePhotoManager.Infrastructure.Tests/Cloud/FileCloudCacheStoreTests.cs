using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using HanabePhotoManager.Infrastructure.Cloud;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class FileCloudCacheStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "HanabeCloudCacheTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PutAndGet_RoundTripsUnicodeKeyAcrossInstancesAtHashedPath()
    {
        const string key = @"夸克:C:\照片\07.14\JK0001.JPG?size=large";
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        var stream = new MemoryStream([1, 2, 3, 4]);

        var writtenPath = await new FileCloudCacheStore(_root, () => Timestamp(1))
            .PutAsync(key, stream, pinned: false);
        var restoredPath = await new FileCloudCacheStore(_root, () => Timestamp(2))
            .TryGetAsync(key);

        writtenPath.Should().Be(Path.Combine(_root, "content", expectedHash));
        restoredPath.Should().Be(writtenPath);
        (await File.ReadAllBytesAsync(restoredPath!)).Should().BeEquivalentTo([1, 2, 3, 4]);
        Path.GetFileName(writtenPath).Should().NotContain(key);
    }

    [Fact]
    public async Task PutAsync_ReadsFromCurrentPositionAndLeavesNonSeekableStreamOpen()
    {
        var stream = new TrackingNonSeekableStream([9, 8, 1, 2, 3], initialPosition: 2);
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        var path = await store.PutAsync("position", stream, pinned: false);

        (await File.ReadAllBytesAsync(path)).Should().BeEquivalentTo([1, 2, 3]);
        stream.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task PutAsync_ReplacesExistingKeyAndUpdatesPinnedState()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        var firstPath = await store.PutAsync("same", new MemoryStream([1, 2]), pinned: true);
        var secondPath = await store.PutAsync("same", new MemoryStream([3, 4, 5]), pinned: false);

        secondPath.Should().Be(firstPath);
        (await File.ReadAllBytesAsync(secondPath)).Should().BeEquivalentTo([3, 4, 5]);

        await store.TrimAsync(0);
        (await store.TryGetAsync("same")).Should().BeNull();
    }

    [Fact]
    public async Task DifferentKeys_UseDifferentHashedPaths()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        var first = await store.PutAsync("A", new MemoryStream([1]), false);
        var second = await store.PutAsync("a", new MemoryStream([2]), false);

        first.Should().NotBe(second);
        Path.GetDirectoryName(first).Should().Be(Path.GetDirectoryName(second));
    }

    [Fact]
    public async Task TrimAsync_RemovesLeastRecentlyAccessedUnpinnedEntriesAndPreservesPinned()
    {
        var clock = new FakeClock();
        var store = new FileCloudCacheStore(_root, clock.UtcNow);
        await store.PutAsync("old", new MemoryStream(new byte[8]), false);
        clock.Advance();
        await store.PutAsync("pinned", new MemoryStream(new byte[8]), true);
        clock.Advance();
        await store.PutAsync("new", new MemoryStream(new byte[8]), false);

        await store.TrimAsync(16);

        (await store.TryGetAsync("old")).Should().BeNull();
        (await store.TryGetAsync("pinned")).Should().NotBeNull();
        (await store.TryGetAsync("new")).Should().NotBeNull();
    }

    [Fact]
    public async Task TryGetAsync_UpdatesPersistedLastAccessedTimeUsedByLru()
    {
        var clock = new FakeClock();
        var store = new FileCloudCacheStore(_root, clock.UtcNow);
        await store.PutAsync("first", new MemoryStream(new byte[4]), false);
        clock.Advance();
        await store.PutAsync("second", new MemoryStream(new byte[4]), false);
        clock.Advance();
        (await store.TryGetAsync("first")).Should().NotBeNull();

        await new FileCloudCacheStore(_root, clock.UtcNow).TrimAsync(4);

        (await store.TryGetAsync("first")).Should().NotBeNull();
        (await store.TryGetAsync("second")).Should().BeNull();
    }

    [Fact]
    public async Task TrimAsync_WhenTimestampsTie_UsesStableKeyOrder()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        await store.PutAsync("b", new MemoryStream([2]), false);
        await store.PutAsync("a", new MemoryStream([1]), false);

        await store.TrimAsync(1);

        (await store.TryGetAsync("a")).Should().BeNull();
        (await store.TryGetAsync("b")).Should().NotBeNull();
    }

    [Fact]
    public async Task TrimAsync_WhenPinnedContentAloneExceedsLimit_PreservesIt()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        await store.PutAsync("pinned", new MemoryStream(new byte[8]), true);
        await store.PutAsync("ordinary", new MemoryStream(new byte[2]), false);

        await store.TrimAsync(1);

        (await store.TryGetAsync("pinned")).Should().NotBeNull();
        (await store.TryGetAsync("ordinary")).Should().BeNull();
    }

    [Fact]
    public async Task MissingIndex_BehavesAsEmptyCache()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        (await store.TryGetAsync("missing")).Should().BeNull();
        await store.TrimAsync(0);

        File.Exists(Path.Combine(_root, "cache-index.json")).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task Operations_RejectBlankKeys(string key)
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        var get = () => store.TryGetAsync(key);
        var put = () => store.PutAsync(key, new MemoryStream(), false);

        await get.Should().ThrowAsync<ArgumentException>();
        await put.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TrimAsync_RejectsNegativeMaximum()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        var act = () => store.TrimAsync(-1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task CorruptIndex_FailsExplicitly(string json)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "cache-index.json"), json);
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));

        var act = () => store.TryGetAsync("key");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*cache-index.json*");
    }

    [Theory]
    [InlineData("key")]
    [InlineData("relativePath")]
    [InlineData("size")]
    [InlineData("lastAccessedAt")]
    [InlineData("pinned")]
    public async Task MissingRequiredIndexField_FailsExplicitly(string field)
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        await store.PutAsync("key", new MemoryStream([1]), false);
        var indexPath = Path.Combine(_root, "cache-index.json");
        var json = JsonNode.Parse(await File.ReadAllTextAsync(indexPath))!.AsArray();
        json[0]!.AsObject().Remove(field).Should().BeTrue();
        await File.WriteAllTextAsync(indexPath, json.ToJsonString());

        var act = () => new FileCloudCacheStore(_root, () => Timestamp(2)).TryGetAsync("key");

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*cache-index.json*");
    }

    [Fact]
    public async Task PathEscapingIndex_FailsExplicitlyWithoutReadingOutsideRoot()
    {
        Directory.CreateDirectory(_root);
        var outsidePath = Path.Combine(Path.GetDirectoryName(_root)!, "outside-secret.txt");
        await File.WriteAllTextAsync(outsidePath, "secret");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "cache-index.json"),
            $$"""
            [
              {
                "key": "key",
                "relativePath": "../{{Path.GetFileName(outsidePath)}}",
                "size": 6,
                "lastAccessedAt": "2026-07-16T00:00:00+00:00",
                "pinned": false
              }
            ]
            """);

        var act = () => new FileCloudCacheStore(_root, () => Timestamp(1)).TryGetAsync("key");

        await act.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllTextAsync(outsidePath)).Should().Be("secret");
        File.Delete(outsidePath);
    }

    [Fact]
    public async Task MissingContent_RemovesStaleMetadataAndReturnsNull()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        var path = await store.PutAsync("stale", new MemoryStream([1]), false);
        File.Delete(path);

        (await store.TryGetAsync("stale")).Should().BeNull();

        var index = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(_root, "cache-index.json")))!.AsArray();
        index.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedPut_PreservesOldContentAndIndexAndCleansTemporaryFiles()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        var path = await store.PutAsync("key", new MemoryStream([1, 2, 3]), true);
        var indexPath = Path.Combine(_root, "cache-index.json");
        var originalIndex = await File.ReadAllTextAsync(indexPath);
        var stream = new ThrowingReadStream([9, 9, 9, 9], throwAfterBytes: 2);

        var act = () => store.PutAsync("key", stream, pinned: false);

        await act.Should().ThrowAsync<IOException>();
        (await File.ReadAllBytesAsync(path)).Should().BeEquivalentTo([1, 2, 3]);
        (await File.ReadAllTextAsync(indexPath)).Should().Be(originalIndex);
        TemporaryFiles().Should().BeEmpty();
        stream.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task CanceledPut_PreservesOldContentAndIndexAndCleansTemporaryFiles()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        var path = await store.PutAsync("key", new MemoryStream([1, 2, 3]), true);
        var indexPath = Path.Combine(_root, "cache-index.json");
        var originalIndex = await File.ReadAllTextAsync(indexPath);
        using var cancellation = new CancellationTokenSource();
        var stream = new CancelingReadStream(new byte[128 * 1024], cancellation);

        var act = () => store.PutAsync("key", stream, pinned: false, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await File.ReadAllBytesAsync(path)).Should().BeEquivalentTo([1, 2, 3]);
        (await File.ReadAllTextAsync(indexPath)).Should().Be(originalIndex);
        TemporaryFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentPutsAcrossSameAndDifferentInstances_DoNotLoseIndexEntries()
    {
        var one = new FileCloudCacheStore(_root, () => Timestamp(1));
        var two = new FileCloudCacheStore(_root, () => Timestamp(1));
        var tasks = Enumerable.Range(0, 24)
            .Select(index => (index % 2 == 0 ? one : two).PutAsync(
                $"key-{index}",
                new MemoryStream(BitConverter.GetBytes(index)),
                pinned: false));

        await Task.WhenAll(tasks);

        var restored = new FileCloudCacheStore(_root, () => Timestamp(2));
        for (var index = 0; index < 24; index++)
        {
            var path = await restored.TryGetAsync($"key-{index}");
            path.Should().NotBeNull();
            (await File.ReadAllBytesAsync(path!)).Should().BeEquivalentTo(BitConverter.GetBytes(index));
        }
    }

    [Fact]
    public async Task ExternallyHeldLock_CanBeCanceledWithoutMutation()
    {
        var store = new FileCloudCacheStore(_root, () => Timestamp(1));
        var path = await store.PutAsync("old", new MemoryStream([1]), false);
        var indexPath = Path.Combine(_root, "cache-index.json");
        var originalIndex = await File.ReadAllTextAsync(indexPath);
        await using var externalLock = new FileStream(
            indexPath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var act = () => new FileCloudCacheStore(_root, () => Timestamp(2))
            .PutAsync("new", new MemoryStream([2]), false, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(path).Should().BeTrue();
        (await File.ReadAllTextAsync(indexPath)).Should().Be(originalIndex);
        TemporaryFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task LockOnOneRoot_DoesNotBlockDifferentRoot()
    {
        var first = new FileCloudCacheStore(_root, () => Timestamp(1));
        await first.PutAsync("old", new MemoryStream([1]), false);
        await using var externalLock = new FileStream(
            Path.Combine(_root, "cache-index.json.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        var otherRoot = _root + "-other";

        var path = await new FileCloudCacheStore(otherRoot, () => Timestamp(1))
            .PutAsync("new", new MemoryStream([2]), false)
            .WaitAsync(TimeSpan.FromSeconds(2));

        File.Exists(path).Should().BeTrue();
        Directory.Delete(otherRoot, recursive: true);
    }

    [Fact]
    public void Constructor_RejectsBlankRootAndNullClock()
    {
        var blank = () => new FileCloudCacheStore(" ", () => Timestamp(1));
        var nullClock = () => new FileCloudCacheStore(_root, null!);

        blank.Should().Throw<ArgumentException>();
        nullClock.Should().Throw<ArgumentNullException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string[] TemporaryFiles() =>
        Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories)
            : [];

    private static DateTimeOffset Timestamp(int minute) =>
        DateTimeOffset.Parse("2026-07-16T00:00:00Z").AddMinutes(minute);

    private sealed class FakeClock
    {
        private DateTimeOffset _value = Timestamp(0);
        public DateTimeOffset UtcNow() => _value;
        public void Advance() => _value = _value.AddMinutes(1);
    }

    private class TrackingNonSeekableStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        public TrackingNonSeekableStream(byte[] data, int initialPosition = 0)
        {
            _data = data;
            _position = initialPosition;
        }

        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var copied = Math.Min(count, _data.Length - _position);
            if (copied == 0)
            {
                return 0;
            }

            Array.Copy(_data, _position, buffer, offset, copied);
            _position += copied;
            return copied;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var copied = Math.Min(buffer.Length, _data.Length - _position);
            _data.AsMemory(_position, copied).CopyTo(buffer);
            _position += copied;
            return ValueTask.FromResult(copied);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream(byte[] data, int throwAfterBytes)
        : TrackingNonSeekableStream(data)
    {
        private int _read;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_read >= throwAfterBytes)
            {
                throw new IOException("Injected stream failure.");
            }

            var limited = buffer[..Math.Min(buffer.Length, throwAfterBytes - _read)];
            var read = await base.ReadAsync(limited, cancellationToken);
            _read += read;
            return read;
        }
    }

    private sealed class CancelingReadStream(byte[] data, CancellationTokenSource cancellation)
        : TrackingNonSeekableStream(data)
    {
        private bool _hasRead;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_hasRead)
            {
                cancellation.Cancel();
            }

            _hasRead = true;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 1024)], cancellationToken);
        }
    }
}
