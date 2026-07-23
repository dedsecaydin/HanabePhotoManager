using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class ModelsTests
{
    [Fact]
    public void LibraryDate_BuildsExpectedRelativePath()
    {
        var date = new LibraryDate(2026, 7, 11);

        date.RelativePath.Should().Be(Path.Combine("7月", "07.11"));
    }

    [Fact]
    public void LibraryDate_RejectsInvalidCalendarDate()
    {
        var act = () => new LibraryDate(2026, 2, 30);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LibraryDate_RejectsInvalidYear()
    {
        var act = () => new LibraryDate(0, 7, 11);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LibraryDate_AcceptsLeapDay()
    {
        var date = new LibraryDate(2024, 2, 29);

        date.Year.Should().Be(2024);
        date.Month.Should().Be(2);
        date.Day.Should().Be(29);
    }

    [Fact]
    public void LibraryDate_DefaultValueIsCanonicalMinimumDate()
    {
        var date = default(LibraryDate);

        date.Year.Should().Be(1);
        date.Month.Should().Be(1);
        date.Day.Should().Be(1);
        date.RelativePath.Should().Be(Path.Combine("1月", "01.01"));
    }

    [Fact]
    public void MediaGroup_CopiesSidecarsAtConstruction()
    {
        var primary = CreateSource("photo.jpg");
        var sidecars = new List<SourceMediaFile>();
        var group = new MediaGroup("photo", MediaCategory.Jpeg, primary, sidecars);

        sidecars.Add(CreateSource("photo.xmp"));

        group.Sidecars.Should().BeEmpty();
    }

    [Fact]
    public void ImportPlanItem_CopiesFilesAtConstruction()
    {
        var primary = CreateSource("photo.jpg");
        var group = new MediaGroup("photo", MediaCategory.Jpeg, primary, Array.Empty<SourceMediaFile>());
        var files = new List<PlannedFile>();
        var item = new ImportPlanItem(Guid.NewGuid(), group, files, ConflictKind.None, ImportItemState.Planned);

        files.Add(new PlannedFile(primary, "destination.jpg", "temporary.jpg", ConflictKind.None));

        item.Files.Should().BeEmpty();
    }

    [Fact]
    public void ImportPlan_CopiesItemsAtConstruction()
    {
        var primary = CreateSource("photo.jpg");
        var group = new MediaGroup("photo", MediaCategory.Jpeg, primary, Array.Empty<SourceMediaFile>());
        var item = new ImportPlanItem(
            Guid.NewGuid(),
            group,
            Array.Empty<PlannedFile>(),
            ConflictKind.None,
            ImportItemState.Planned);
        var items = new List<ImportPlanItem>();
        var plan = new ImportPlan("library", new LibraryDate(2026, 7, 11), TransferMode.CopyKeepSource, items);

        items.Add(item);

        plan.Items.Should().BeEmpty();
    }

    private static SourceMediaFile CreateSource(string fullPath)
    {
        return new SourceMediaFile(fullPath, 42, DateTimeOffset.UnixEpoch);
    }
}
