using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class DestinationProbeTests
{
    [Fact]
    public async Task CheckAsync_ReturnsNoneForMissingDestination()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3]);
        var missingDestination = Path.Combine(files.DirectoryPath, "missing.jpg");

        var result = await new DestinationProbe(new Sha256FileHasher())
            .CheckAsync(files.Source, missingDestination, CancellationToken.None);

        result.Should().Be(ConflictKind.None);
    }

    [Fact]
    public async Task CheckAsync_ReturnsDifferentContentWhenDestinationIsDirectory()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3]);
        Directory.CreateDirectory(files.DestinationPath);

        var result = await new DestinationProbe(new Sha256FileHasher())
            .CheckAsync(files.Source, files.DestinationPath, CancellationToken.None);

        result.Should().Be(ConflictKind.SameNameDifferentContent);
    }

    [Fact]
    public async Task CheckAsync_ReturnsDifferentContentForUnequalSize()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3], destinationBytes: [1, 2]);

        var result = await new DestinationProbe(new Sha256FileHasher())
            .CheckAsync(files.Source, files.DestinationPath, CancellationToken.None);

        result.Should().Be(ConflictKind.SameNameDifferentContent);
    }

    [Fact]
    public async Task CheckAsync_ReturnsIdenticalForEqualContent()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3], destinationBytes: [1, 2, 3]);

        var result = await new DestinationProbe(new Sha256FileHasher())
            .CheckAsync(files.Source, files.DestinationPath, CancellationToken.None);

        result.Should().Be(ConflictKind.Identical);
    }

    [Fact]
    public async Task CheckAsync_ReturnsDifferentContentForEqualSizeDifferentContent()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3], destinationBytes: [1, 2, 4]);

        var result = await new DestinationProbe(new Sha256FileHasher())
            .CheckAsync(files.Source, files.DestinationPath, CancellationToken.None);

        result.Should().Be(ConflictKind.SameNameDifferentContent);
    }

    [Fact]
    public async Task CheckAsync_FlowsCancellationTokenToHasher()
    {
        using var files = new ProbeFiles(sourceBytes: [1, 2, 3], destinationBytes: [1, 2, 3]);
        using var cts = new CancellationTokenSource();
        var hasher = new RecordingHasher();

        await new DestinationProbe(hasher).CheckAsync(files.Source, files.DestinationPath, cts.Token);

        hasher.Tokens.Should().Equal(cts.Token, cts.Token);
    }

    [Fact]
    public async Task CheckAsync_RejectsInvalidArguments()
    {
        var probe = new DestinationProbe(new Sha256FileHasher());
        var source = new SourceMediaFile("source.jpg", 1, DateTimeOffset.UnixEpoch);

        await probe.Invoking(p => p.CheckAsync(null!, "destination.jpg", CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
        await probe.Invoking(p => p.CheckAsync(source, "   ", CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await probe.Invoking(p => p.CheckAsync(new SourceMediaFile("   ", 1, DateTimeOffset.UnixEpoch), "destination.jpg", CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    private sealed class RecordingHasher : IFileHasher
    {
        public List<CancellationToken> Tokens { get; } = [];

        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Path.GetFileName(path).StartsWith("source", StringComparison.OrdinalIgnoreCase)
                ? "SOURCE-HASH"
                : "DESTINATION-HASH");
        }
    }

    private sealed class ProbeFiles : IDisposable
    {
        public ProbeFiles(byte[] sourceBytes, byte[]? destinationBytes = null)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"hanabe-probe-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
            SourcePath = Path.Combine(DirectoryPath, "source.jpg");
            DestinationPath = Path.Combine(DirectoryPath, "destination.jpg");
            File.WriteAllBytes(SourcePath, sourceBytes);
            if (destinationBytes is not null)
            {
                File.WriteAllBytes(DestinationPath, destinationBytes);
            }

            Source = new SourceMediaFile(SourcePath, sourceBytes.Length, DateTimeOffset.UnixEpoch);
        }

        public string DirectoryPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public SourceMediaFile Source { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
