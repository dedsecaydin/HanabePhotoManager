using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class VerifiedFileTransferTests
{
    [Fact]
    public async Task TransferGroupAsync_SuccessfulCopyKeepsSourceAndRemovesTemporaryFile()
    {
        using var workspace = new TransferWorkspace();
        var source = workspace.WriteSource("photo.jpg", [1, 2, 3]);
        var file = workspace.Plan(source, "JPG生图", ConflictKind.None);
        var item = CreateItem(file);

        var result = await new VerifiedFileTransfer(new Sha256FileHasher())
            .TransferGroupAsync(item, deleteSourcesAfterVerify: false, CancellationToken.None);

        result.Success.Should().BeTrue(result.Error);
        File.Exists(source.FullPath).Should().BeTrue();
        File.Exists(file.DestinationPath).Should().BeTrue();
        File.Exists(file.TemporaryPath).Should().BeFalse();
        File.ReadAllBytes(file.DestinationPath).Should().Equal(1, 2, 3);
        result.VerifiedFiles.Should().ContainSingle().Which.File.Should().Be(file);
    }

    [Fact]
    public async Task TransferGroupAsync_MoveModeDeletesSourcesOnlyAfterAllMembersVerifiedAndPublished()
    {
        using var workspace = new TransferWorkspace();
        var firstSource = workspace.WriteSource("clip.mp4", [1, 2, 3]);
        var secondSource = workspace.WriteSource("clip.xml", [4, 5]);
        var firstFile = workspace.Plan(firstSource, "视频", ConflictKind.None);
        var secondFile = workspace.Plan(secondSource, "视频", ConflictKind.None);
        var item = CreateItem(firstFile, secondFile);

        var result = await new VerifiedFileTransfer(new Sha256FileHasher())
            .TransferGroupAsync(item, deleteSourcesAfterVerify: true, CancellationToken.None);

        result.Success.Should().BeTrue(result.Error);
        File.Exists(firstFile.DestinationPath).Should().BeTrue();
        File.Exists(secondFile.DestinationPath).Should().BeTrue();
        File.Exists(firstSource.FullPath).Should().BeFalse();
        File.Exists(secondSource.FullPath).Should().BeFalse();
        result.VerifiedFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransferGroupAsync_HashMismatchInSecondMemberLeavesSourcesAndPublishesNoDestinations()
    {
        using var workspace = new TransferWorkspace();
        var firstSource = workspace.WriteSource("a.jpg", [1]);
        var secondSource = workspace.WriteSource("b.jpg", [2]);
        var firstFile = workspace.Plan(firstSource, "JPG生图", ConflictKind.None);
        var secondFile = workspace.Plan(secondSource, "JPG生图", ConflictKind.None);
        var hasher = new MismatchForPathHasher(secondFile.TemporaryPath);

        var result = await new VerifiedFileTransfer(hasher)
            .TransferGroupAsync(CreateItem(firstFile, secondFile), deleteSourcesAfterVerify: true, CancellationToken.None);

        result.Success.Should().BeFalse();
        File.Exists(firstSource.FullPath).Should().BeTrue();
        File.Exists(secondSource.FullPath).Should().BeTrue();
        File.Exists(firstFile.DestinationPath).Should().BeFalse();
        File.Exists(secondFile.DestinationPath).Should().BeFalse();
    }

    [Fact]
    public async Task TransferGroupAsync_SameNameDifferentContentFailsBeforeCopying()
    {
        using var workspace = new TransferWorkspace();
        var source = workspace.WriteSource("photo.jpg", [1]);
        var file = workspace.Plan(source, "JPG生图", ConflictKind.SameNameDifferentContent);

        var result = await new VerifiedFileTransfer(new Sha256FileHasher())
            .TransferGroupAsync(CreateItem(file), deleteSourcesAfterVerify: true, CancellationToken.None);

        result.Success.Should().BeFalse();
        File.Exists(source.FullPath).Should().BeTrue();
        File.Exists(file.DestinationPath).Should().BeFalse();
        File.Exists(file.TemporaryPath).Should().BeFalse();
        result.VerifiedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task TransferGroupAsync_IdenticalConflictVerifiesHashesAndSkipsCopy()
    {
        using var workspace = new TransferWorkspace();
        var source = workspace.WriteSource("photo.jpg", [1, 2, 3]);
        var file = workspace.Plan(source, "JPG生图", ConflictKind.Identical);
        Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);
        await File.WriteAllBytesAsync(file.DestinationPath, [1, 2, 3]);
        var hasher = new RecordingRealHasher();

        var result = await new VerifiedFileTransfer(hasher)
            .TransferGroupAsync(CreateItem(file), deleteSourcesAfterVerify: false, CancellationToken.None);

        result.Success.Should().BeTrue(result.Error);
        File.Exists(file.TemporaryPath).Should().BeFalse();
        hasher.Paths.Should().Equal(file.DestinationPath);
        File.ReadAllBytes(file.DestinationPath).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task TransferGroupAsync_PublicationFailureLeavesSourcesIntactAndReportsVerifiedFiles()
    {
        using var workspace = new TransferWorkspace();
        var firstSource = workspace.WriteSource("a.jpg", [1]);
        var secondSource = workspace.WriteSource("b.jpg", [2]);
        var firstFile = workspace.Plan(firstSource, "JPG生图", ConflictKind.None);
        var secondFile = workspace.Plan(secondSource, "JPG生图", ConflictKind.None);
        Directory.CreateDirectory(secondFile.DestinationPath);

        var result = await new VerifiedFileTransfer(new Sha256FileHasher())
            .TransferGroupAsync(CreateItem(firstFile, secondFile), deleteSourcesAfterVerify: true, CancellationToken.None);

        result.Success.Should().BeFalse();
        File.Exists(firstSource.FullPath).Should().BeTrue();
        File.Exists(secondSource.FullPath).Should().BeTrue();
        File.Exists(firstFile.DestinationPath).Should().BeFalse();
        File.Exists(firstFile.TemporaryPath).Should().BeFalse();
        File.Exists(secondFile.TemporaryPath).Should().BeFalse();
        result.VerifiedFiles.Should().HaveCount(2);
    }

    private static ImportPlanItem CreateItem(params PlannedFile[] files)
    {
        var primary = files[0].Source;
        var sidecars = files.Skip(1).Select(file => file.Source).ToArray();
        var group = new MediaGroup("group", MediaCategory.Jpeg, primary, sidecars);
        return new ImportPlanItem(Guid.NewGuid(), group, files, ConflictKind.None, ImportItemState.Planned);
    }

    private sealed class TransferWorkspace : IDisposable
    {
        public TransferWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"hanabe-transfer-{Guid.NewGuid():N}");
            SourceRoot = Path.Combine(Root, "source");
            LibraryRoot = Path.Combine(Root, "library");
            Directory.CreateDirectory(SourceRoot);
        }

        public string Root { get; }

        public string SourceRoot { get; }

        public string LibraryRoot { get; }

        public SourceMediaFile WriteSource(string fileName, byte[] bytes)
        {
            var path = Path.Combine(SourceRoot, fileName);
            File.WriteAllBytes(path, bytes);
            return new SourceMediaFile(path, bytes.Length, DateTimeOffset.UtcNow);
        }

        public PlannedFile Plan(SourceMediaFile source, string category, ConflictKind conflict)
        {
            var destination = Path.Combine(LibraryRoot, "2026", "7月", "07.11", category, Path.GetFileName(source.FullPath));
            return new PlannedFile(source, destination, destination + ".hanabe-part", conflict);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class MismatchForPathHasher(string mismatchPath) : IFileHasher
    {
        private readonly Sha256FileHasher _inner = new();

        public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            var hash = await _inner.ComputeSha256Async(path, cancellationToken);
            return string.Equals(path, mismatchPath, StringComparison.OrdinalIgnoreCase) ? "0" + hash[1..] : hash;
        }
    }

    private sealed class RecordingRealHasher : IFileHasher
    {
        private readonly Sha256FileHasher _inner = new();

        public List<string> Paths { get; } = [];

        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            Paths.Add(path);
            return _inner.ComputeSha256Async(path, cancellationToken);
        }
    }

    private sealed class MutateSourceBeforeDeleteHasher(string sourcePath) : IFileHasher
    {
        private readonly Sha256FileHasher _inner = new();
        private int _sourceHashCount;

        public bool MutationBlocked { get; private set; }

        public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            if (string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                _sourceHashCount++;
                if (_sourceHashCount == 2)
                {
                    try
                    {
                        await File.WriteAllBytesAsync(path, [9, 9, 9], cancellationToken);
                    }
                    catch (IOException)
                    {
                        MutationBlocked = true;
                    }
                }
            }

            return await _inner.ComputeSha256Async(path, cancellationToken);
        }
    }

    private sealed class ReplaceSourceAfterVerificationHasher(string sourcePath, string backupPath) : IFileHasher
    {
        private readonly Sha256FileHasher _inner = new();
        private int _sourceHashCount;

        public bool MutationBlocked { get; private set; }

        public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            var hash = await _inner.ComputeSha256Async(path, cancellationToken);
            if (string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                _sourceHashCount++;
                if (_sourceHashCount == 2)
                {
                    try
                    {
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        File.Move(path, backupPath);
                        await File.WriteAllBytesAsync(path, [9, 9, 9], cancellationToken);
                    }
                    catch (IOException)
                    {
                        MutationBlocked = true;
                    }
                }
            }

            return hash;
        }
    }
}
