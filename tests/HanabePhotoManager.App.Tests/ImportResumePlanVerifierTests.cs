using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportResumePlanVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabeResume", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PartialMove_RestartTransfersRemainingSidecarToOriginalTarget()
    {
        var entry = CreateEntry();
        var sidecarPath = Path.Combine(_root, "source.xml");
        File.WriteAllBytes(sidecarPath, [4, 5]);
        var sidecar = new SourceMediaFile(sidecarPath, 2, DateTimeOffset.UtcNow);
        var target = Path.Combine(_root, "001.xml");
        var group = new MediaGroup("source", MediaCategory.Jpeg, entry.Plan!.Group.Primary, [sidecar]);
        entry.Plan = new ImportPlanItem(Guid.NewGuid(), group,
            [entry.Plan.Files[0], new PlannedFile(sidecar, target, target + ".hanabe-part", ConflictKind.None)],
            ConflictKind.None, ImportItemState.Planned);
        var hasher = new Sha256FileHasher();
        var transfer = new VerifiedFileTransfer(hasher);
        (await transfer.TransferGroupAsync(entry.Plan, false, CancellationToken.None,
            receipts => entry.VerifiedFiles = receipts.ToList())).Success.Should().BeTrue();
        File.Delete(entry.PrimaryPath);
        var store = new ImportResumeStore(Path.Combine(_root, "resume.json"));
        store.Save(new ImportResumeState { Entries = [entry], DeleteSourcesAfterVerify = true });
        var remaining = await ImportResumePlanVerifier.ReconcileAsync(store.Load()!.Entries.Single(), hasher, CancellationToken.None);
        remaining.Files.Should().ContainSingle().Which.DestinationPath.Should().Be(target);
        (await transfer.TransferGroupAsync(remaining, true, CancellationToken.None)).Success.Should().BeTrue();
        File.Exists(sidecarPath).Should().BeFalse();
        File.ReadAllBytes(target).Should().Equal(4, 5);
    }

    [Fact]
    public async Task Restart_PreservesPlannedNameAndDetectsAlreadyCopiedFile()
    {
        var entry = CreateEntry();
        var file = entry.Plan!.Files[0];
        File.Copy(file.Source.FullPath, file.DestinationPath);
        var store = new ImportResumeStore(Path.Combine(_root, "resume.json"));
        store.Save(new ImportResumeState { Entries = [entry] });
        var restored = store.Load()!.Entries.Single();
        var result = await ImportResumePlanVerifier.ReconcileAsync(restored, new Sha256FileHasher(), CancellationToken.None);
        result.Files.Single().DestinationPath.Should().Be(file.DestinationPath);
        result.Conflict.Should().Be(ConflictKind.Identical);
    }

    [Fact]
    public async Task MissingSourceWithoutReceipt_IsNotConsideredComplete()
    {
        var entry = CreateEntry();
        File.Delete(entry.PrimaryPath);
        await Assert.ThrowsAsync<IOException>(() => ImportResumePlanVerifier.ReconcileAsync(entry, new Sha256FileHasher(), CancellationToken.None));
    }

    [Fact]
    public async Task DeletedSourceWithDurableReceipt_IsCompleteOnlyWhileDestinationMatches()
    {
        var entry = CreateEntry();
        var hasher = new Sha256FileHasher();
        var result = await new VerifiedFileTransfer(hasher).TransferGroupAsync(entry.Plan!, true, CancellationToken.None,
            verified => entry.VerifiedFiles = verified.ToList());
        result.Success.Should().BeTrue();
        var store = new ImportResumeStore(Path.Combine(_root, "resume.json"));
        store.Save(new ImportResumeState { Entries = [entry] });
        entry = store.Load()!.Entries.Single();
        (await ImportResumePlanVerifier.ReconcileAsync(entry, hasher, CancellationToken.None)).Files.Should().BeEmpty();
        File.WriteAllBytes(entry.Plan!.Files[0].DestinationPath, [9, 9, 9]);
        await Assert.ThrowsAsync<IOException>(() => ImportResumePlanVerifier.ReconcileAsync(entry, hasher, CancellationToken.None));
    }

    [Fact]
    public async Task DestinationChangedAfterCheckpoint_IsConflict()
    {
        var entry = CreateEntry();
        File.WriteAllBytes(entry.Plan!.Files[0].DestinationPath, [9, 9, 9]);
        var result = await ImportResumePlanVerifier.ReconcileAsync(entry, new Sha256FileHasher(), CancellationToken.None);
        result.Conflict.Should().Be(ConflictKind.SameNameDifferentContent);
    }

    private ImportResumeEntry CreateEntry()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "source.jpg");
        File.WriteAllBytes(path, [1, 2, 3]);
        var source = new SourceMediaFile(path, 3, DateTimeOffset.UtcNow);
        var destination = Path.Combine(_root, "001.jpg");
        var group = new MediaGroup("source", MediaCategory.Jpeg, source, []);
        return new ImportResumeEntry
        {
            PrimaryPath = path, GroupKey = "source", Category = "Jpeg",
            Plan = new ImportPlanItem(Guid.NewGuid(), group,
                [new PlannedFile(source, destination, destination + ".hanabe-part", ConflictKind.None)],
                ConflictKind.None, ImportItemState.Planned)
        };
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
