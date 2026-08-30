using FluentAssertions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class OriginMetadataDuplicateMatcherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hanabe-origin-match-" + Guid.NewGuid().ToString("N"));

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
