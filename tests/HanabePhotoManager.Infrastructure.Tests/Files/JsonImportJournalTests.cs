using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class JsonImportJournalTests
{
    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripImportPlan()
    {
        using var workspace = new JournalWorkspace();
        var path = Path.Combine(workspace.Root, "journal.json");
        var plan = CreatePlan(workspace.Root);
        var journal = new JsonImportJournal();

        await journal.SaveAsync(plan, path, CancellationToken.None);
        var loaded = await journal.LoadAsync(path, CancellationToken.None);

        loaded.Should().BeEquivalentTo(plan);
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_MissingFileReturnsNull()
    {
        using var workspace = new JournalWorkspace();

        var loaded = await new JsonImportJournal()
            .LoadAsync(Path.Combine(workspace.Root, "missing.json"), CancellationToken.None);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_FailedReplaceDoesNotCorruptExistingFinalFile()
    {
        using var workspace = new JournalWorkspace();
        var path = Path.Combine(workspace.Root, "journal.json");
        await File.WriteAllTextAsync(path, "original");
        Directory.CreateDirectory(path + ".tmp");

        var act = () => new JsonImportJournal().SaveAsync(CreatePlan(workspace.Root), path, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().Where(exception => exception.GetType() == typeof(IOException) || exception.GetType() == typeof(UnauthorizedAccessException));
        File.ReadAllText(path).Should().Be("original");
    }

    [Fact]
    public async Task SaveAsync_RejectsDestinationPathEscapingLibraryRoot()
    {
        using var workspace = new JournalWorkspace();
        var source = new SourceMediaFile(Path.Combine(workspace.Root, "source.jpg"), 3, DateTimeOffset.UnixEpoch);
        var escapingFile = new PlannedFile(
            source,
            Path.Combine(workspace.Root, "..", "outside.jpg"),
            Path.Combine(workspace.Root, "..", "outside.jpg.hanabe-part"),
            ConflictKind.None);
        var group = new MediaGroup("source", MediaCategory.Jpeg, source, Array.Empty<SourceMediaFile>());
        var item = new ImportPlanItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), group, [escapingFile], ConflictKind.None, ImportItemState.Planned);
        var escapingPlan = new ImportPlan(workspace.Root, new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [item]);

        var act = () => new JsonImportJournal()
            .SaveAsync(escapingPlan, Path.Combine(workspace.Root, "journal.json"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private static ImportPlan CreatePlan(string root)
    {
        var source = new SourceMediaFile(Path.Combine(root, "source.jpg"), 3, DateTimeOffset.UnixEpoch);
        var group = new MediaGroup("source", MediaCategory.Jpeg, source, Array.Empty<SourceMediaFile>());
        var destination = Path.Combine(root, "dest.jpg");
        var file = new PlannedFile(source, destination, destination + ".hanabe-part", ConflictKind.None);
        var item = new ImportPlanItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), group, [file], ConflictKind.None, ImportItemState.Planned);
        return new ImportPlan(root, new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, [item]);
    }

    private sealed class JournalWorkspace : IDisposable
    {
        public JournalWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"hanabe-journal-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}


