using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class CachedFileHasherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabeHashCache", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DisabledAndClearedCache_RecomputesContent()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "a.jpg");
        File.WriteAllBytes(file, [1, 2, 3]);
        var inner = new CountingHasher();
        var cache = new CachedFileHasher(inner, Path.Combine(_root, "cache.jsonl"));
        await cache.ComputeSha256Async(file, default);
        cache.Enabled = false;
        await cache.ComputeSha256Async(file, default);
        cache.Enabled = true;
        cache.Clear();
        await cache.ComputeSha256Async(file, default);
        inner.Calls.Should().Be(3);
    }

    [Fact]
    public async Task CanceledFingerprint_IsNotPersisted()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "a.jpg");
        File.WriteAllBytes(file, [1, 2, 3]);
        var cachePath = Path.Combine(_root, "cache.jsonl");
        var cache = new CachedFileHasher(new CountingHasher(), cachePath);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrComputeAsync(file, "test", _ =>
        {
            cancellation.Cancel();
            return Task.FromResult("incomplete");
        }, cancellation.Token));
        File.Exists(cachePath).Should().BeFalse();
    }

    [Fact]
    public async Task ChangedDuringHash_DoesNotPublishStaleFingerprint()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "a.jpg");
        File.WriteAllBytes(file, [1]);
        var cache = new CachedFileHasher(new CountingHasher(), Path.Combine(_root, "cache.jsonl"));
        await Assert.ThrowsAsync<IOException>(() => cache.GetOrComputeAsync(file, "test", _ =>
        {
            File.WriteAllBytes(file, [1, 2]);
            return Task.FromResult("stale");
        }, default));
    }

    [Fact]
    public async Task Restart_ReusesPreviousScanAndRehashesChangedFile()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "a.jpg");
        var cachePath = Path.Combine(_root, "cache.jsonl");
        File.WriteAllBytes(file, [1, 2, 3]);
        var inner = new CountingHasher();
        var first = await new CachedFileHasher(inner, cachePath).ComputeSha256Async(file, default);
        var restarted = new CachedFileHasher(inner, cachePath);
        (await restarted.ComputeSha256Async(file, default)).Should().Be(first);
        inner.Calls.Should().Be(1);
        File.WriteAllBytes(file, [4, 5, 6]);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(5));
        (await restarted.ComputeSha256Async(file, default)).Should().NotBe(first);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task RecentImportReceipt_IsReusedWithoutReadingContentAgain()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "a.jpg");
        File.WriteAllBytes(file, [1, 2, 3]);
        var inner = new CountingHasher();
        var cache = new CachedFileHasher(inner, Path.Combine(_root, "cache.jsonl"));
        var hash = await new Sha256FileHasher().ComputeSha256Async(file, default);
        cache.RememberVerified(file, hash);
        (await cache.ComputeSha256Async(file, default)).Should().Be(hash);
        inner.Calls.Should().Be(0);
    }

    private sealed class CountingHasher : IFileHasher
    {
        public int Calls { get; private set; }
        public Task<string> ComputeSha256Async(string path, CancellationToken token)
        {
            Calls++;
            return new Sha256FileHasher().ComputeSha256Async(path, token);
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
