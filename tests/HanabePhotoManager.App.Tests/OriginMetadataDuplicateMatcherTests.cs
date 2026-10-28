using FluentAssertions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using System.IO;
using Xunit;
using HanabePhotoManager.Infrastructure.Files;
using System.Diagnostics;
using System.Text.Json;

namespace HanabePhotoManager.App.Tests;

public sealed class OriginMetadataDuplicateMatcherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hanabe-origin-match-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PersistentMetadataIndex_ReusesUnchangedFilesAndInvalidatesChangedFiles()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source.JPG");
        var candidate = Path.Combine(_directory, "existing.JPG");
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(candidate, "existing");
        var path = Path.Combine(_directory, "cache.jsonl");
        var store = new CountingStore();
        for (var index = 0; index < 2; index++)
            await new OriginMetadataDuplicateMatcher(store, new CachedFileHasher(new FakeHasher("A"), path))
                .FindMatchAsync(source, new[] { candidate }, default);
        Assert.Equal(1, store.Reads);
        await File.WriteAllTextAsync(candidate, "changed with a new length");
        await new OriginMetadataDuplicateMatcher(store, new CachedFileHasher(new FakeHasher("A"), path))
            .FindMatchAsync(source, new[] { candidate }, default);
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task BatchBenchmark_ReducesRepeatedMetadataReads()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source.JPG");
        await File.WriteAllTextAsync(source, "source");
        var candidates = Enumerable.Range(0, 40).Select(index => Path.Combine(_directory, $"{index}.JPG")).ToArray();
        foreach (var candidate in candidates) await File.WriteAllTextAsync(candidate, "existing");
        var store = new CountingStore { DelayMilliseconds = 1 };
        var matcher = new OriginMetadataDuplicateMatcher(store, new FakeHasher("A"));
        var watch = Stopwatch.StartNew();
        for (var index = 0; index < 20; index++)
        {
            matcher.BeginBatch(); // 模拟旧实现对每个来源都重新读取所有目标备注。
            await matcher.FindMatchAsync(source, candidates, default);
        }
        var baselineMs = watch.Elapsed.TotalMilliseconds;
        var baselineReads = store.Reads;
        store.Reads = 0;
        matcher.BeginBatch();
        watch.Restart();
        for (var index = 0; index < 20; index++) await matcher.FindMatchAsync(source, candidates, default);
        var optimizedMs = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(800, baselineReads);
        Assert.Equal(40, store.Reads);
        var evidence = @"D:\HanabePhoto\.artifacts\heif-speed-20261023";
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "metadata-benchmark.json"), JsonSerializer.Serialize(new
        {
            Scenario = "Controlled fixture: 20 queries, 40 targets, simulated 1ms metadata read latency; not a real network benchmark",
            BaselineReads = baselineReads, OptimizedReads = store.Reads, BaselineMs = baselineMs, OptimizedMs = optimizedMs
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task BatchLookup_ReadsEachCandidateOnlyOnce()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "DSC_1234.JPG");
        var candidate = Path.Combine(_directory, "JK0001.JPG");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(candidate, "destination");
        var store = new CountingStore();
        var matcher = new OriginMetadataDuplicateMatcher(store, new FakeHasher(new string('A', 64)));
        string[] candidates = [candidate];
        for (var index = 0; index < 20; index++)
            await matcher.FindMatchAsync(source, candidates, default);
        Assert.Equal(1, store.Reads);
    }

    private sealed class CountingStore : IFileOriginMetadataStore
    {
        public int Reads;
        public int DelayMilliseconds;
        public async Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken)
        { Reads++; if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds, cancellationToken); return null; }
        public Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata value, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task FindMatchAsync_RequiresNameLengthAndOriginalHash()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "DSC_1234.JPG");
        var candidate = Path.Combine(_directory, "JK0001.JPG");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(candidate, "destination with metadata");
        var hash = new string('D', 64);
        var store = new FakeStore(new FileOriginMetadata("DSC_1234.JPG", new FileInfo(source).Length, hash));
        var matcher = new OriginMetadataDuplicateMatcher(store, new FakeHasher(hash));

        (await matcher.FindMatchAsync(source, [candidate], default)).Should().Be(candidate);
    }

    [Fact]
    public async Task FindMatchAsync_DoesNotMatchSameNameWithDifferentHash()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "DSC_1234.JPG");
        var candidate = Path.Combine(_directory, "JK0001.JPG");
        await File.WriteAllTextAsync(source, "incoming");
        await File.WriteAllTextAsync(candidate, "destination");
        var metadata = new FileOriginMetadata("DSC_1234.JPG", new FileInfo(source).Length, new string('A', 64));
        var matcher = new OriginMetadataDuplicateMatcher(new FakeStore(metadata), new FakeHasher(new string('B', 64)));

        (await matcher.FindMatchAsync(source, [candidate], default)).Should().BeNull();
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    private sealed class FakeStore(FileOriginMetadata? metadata) : IFileOriginMetadataStore
    {
        public Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(metadata);
        public Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata value, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeHasher(string hash) : IFileHasher
    {
        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken) => Task.FromResult(hash);
    }
}
