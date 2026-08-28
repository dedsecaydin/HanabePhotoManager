using FluentAssertions;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class LibraryDateFolderServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-date-folder-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("07.27_鬼灭", 7, 27, "_鬼灭", "07.27_鬼灭")]
    [InlineData("7.4-活动", 7, 4, "-活动", "07.04-活动")]
    [InlineData("7月4日 夜景", 7, 4, " 夜景", "07.04 夜景")]
    [InlineData("7-4", 7, 4, "", "07.04")]
    public void TryParseName_OnlyReadsTheLeadingDateAndPreservesTheSuffix(
        string name,
        int month,
        int day,
        string suffix,
        string normalizedName)
    {
        LibraryDateFolderService.TryParseName(name, expectedMonth: month, out var parsed).Should().BeTrue();

        parsed.Month.Should().Be(month);
        parsed.Day.Should().Be(day);
        parsed.Suffix.Should().Be(suffix);
        parsed.NormalizedName.Should().Be(normalizedName);
    }

    [Fact]
    public void NormalizeDirectoryName_PadsMonthAndDayWithoutOverwritingAnExistingFolder()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "07.04"));
        var source = Directory.CreateDirectory(Path.Combine(_root, "7.4")).FullName;
        File.WriteAllText(Path.Combine(source, "photo.jpg"), "content");
        LibraryDateFolderService.TryParseName("7.4", expectedMonth: 7, out var parsed).Should().BeTrue();

        var effectivePath = LibraryDateFolderService.NormalizeDirectoryName(source, parsed);

        Path.GetFileName(effectivePath).Should().Be("07.04_2");
        Directory.Exists(source).Should().BeFalse();
        File.Exists(Path.Combine(effectivePath, "photo.jpg")).Should().BeTrue();
    }

    [Fact]
    public void Scan_ReturnsChronologicalDirectDateDirectoriesWithoutRenaming()
    {
        var august = Directory.CreateDirectory(Path.Combine(_root, "08月")).FullName;
        var july = Directory.CreateDirectory(Path.Combine(_root, "7月")).FullName;
        var julyDate = Directory.CreateDirectory(Path.Combine(july, "7.4-活动")).FullName;
        var augustDate = Directory.CreateDirectory(Path.Combine(august, "08.28__婚礼--")).FullName;
        Directory.CreateDirectory(Path.Combine(augustDate, "08.29_嵌套目录"));
        Directory.CreateDirectory(Path.Combine(august, "not-a-date"));

        var entries = LibraryDateFolderService.Scan(_root);

        entries.Should().SatisfyRespectively(
            entry =>
            {
                entry.Month.Should().Be(7);
                entry.Day.Should().Be(4);
                entry.Remark.Should().Be("活动");
                entry.FullPath.Should().Be(julyDate);
            },
            entry =>
            {
                entry.Month.Should().Be(8);
                entry.Day.Should().Be(28);
                entry.Remark.Should().Be("婚礼");
                entry.FullPath.Should().Be(augustDate);
            });
        Directory.Exists(augustDate).Should().BeTrue();
        Directory.Exists(Path.Combine(augustDate, "08.29_嵌套目录")).Should().BeTrue();
    }

    [Fact]
    public void RenameRemark_ReturnsSourceMissingInsteadOfSuccess()
    {
        var result = LibraryDateFolderService.RenameRemark(Path.Combine(_root, "08月", "08.28"), "婚礼");

        result.Status.Should().Be(DateFolderRenameStatus.SourceMissing);
    }

    [Fact]
    public void RenameRemark_DoesNotOverwriteExistingTarget()
    {
        var month = Directory.CreateDirectory(Path.Combine(_root, "08月")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(month, "08.28")).FullName;
        Directory.CreateDirectory(Path.Combine(month, "08.28_婚礼"));

        LibraryDateFolderService.RenameRemark(source, "婚礼").Status
            .Should().Be(DateFolderRenameStatus.TargetExists);
        Directory.Exists(source).Should().BeTrue();
    }

    [Fact]
    public void RenameRemark_DoesNotOverwriteAFileAtTheTargetPath()
    {
        var month = Directory.CreateDirectory(Path.Combine(_root, "08月")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(month, "08.28")).FullName;
        var target = Path.Combine(month, "08.28_婚礼");
        File.WriteAllText(target, "existing file");

        var result = LibraryDateFolderService.RenameRemark(source, "婚礼");

        result.Status.Should().Be(DateFolderRenameStatus.TargetExists);
        Directory.Exists(source).Should().BeTrue();
        File.ReadAllText(target).Should().Be("existing file");
    }

    [Fact]
    public void RenameRemark_CanClearAnExistingRemark()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "08月", "08.28_婚礼")).FullName;

        var result = LibraryDateFolderService.RenameRemark(source, "");

        result.Status.Should().Be(DateFolderRenameStatus.Success);
        Path.GetFileName(result.EffectivePath).Should().Be("08.28");
        result.EffectiveRemark.Should().BeEmpty();
    }

    [Fact]
    public void RenameRemark_ReturnsNoChangeWhenTheNormalizedNameAlreadyMatches()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "08月", "08.28_婚礼")).FullName;

        var result = LibraryDateFolderService.RenameRemark(source, "婚礼");

        result.Status.Should().Be(DateFolderRenameStatus.NoChange);
        result.EffectivePath.Should().Be(source);
        result.EffectiveRemark.Should().Be("婚礼");
        Directory.Exists(source).Should().BeTrue();
    }

    [Theory]
    [InlineData("7.4-活动")]
    [InlineData("7月4日 夜景")]
    public void RenameRemark_ReturnsNoChangeWhenSavingTheUnchangedRemarkFromScan(string folderName)
    {
        var month = Directory.CreateDirectory(Path.Combine(_root, "07月")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(month, folderName)).FullName;
        var entry = LibraryDateFolderService.Scan(_root).Should().ContainSingle().Which;

        var result = LibraryDateFolderService.RenameRemark(entry.FullPath, entry.Remark);

        result.Status.Should().Be(DateFolderRenameStatus.NoChange);
        result.EffectivePath.Should().Be(source);
        Directory.Exists(source).Should().BeTrue();
    }

    [Fact]
    public void RenameRemark_SanitizesTheRemarkBeforeMoving()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "08月", "08.28")).FullName;

        var result = LibraryDateFolderService.RenameRemark(source, " _ 婚礼:晚宴 - ");

        result.Status.Should().Be(DateFolderRenameStatus.Success);
        Path.GetFileName(result.EffectivePath).Should().Be("08.28_婚礼晚宴");
        result.EffectiveRemark.Should().Be("婚礼晚宴");
    }

    [Fact]
    public void RenameRemark_TrimsWindowsDiscardedTrailingCharactersAndReturnsTheActualDirectory()
    {
        var month = Directory.CreateDirectory(Path.Combine(_root, "08月")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(month, "08.28")).FullName;

        var result = LibraryDateFolderService.RenameRemark(source, "婚礼. \t");

        result.Status.Should().Be(DateFolderRenameStatus.Success);
        result.EffectivePath.Should().Be(Path.Combine(month, "08.28_婚礼"));
        result.EffectiveRemark.Should().Be("婚礼");
        Directory.GetDirectories(month).Should().ContainSingle().Which.Should().Be(result.EffectivePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    public void RenameRemark_RejectsBlankOrInvalidSourcePathsWithoutFilesystemActions(string sourcePath)
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "08月", "08.28")).FullName;

        DateFolderRenameResult? result = null;
        var action = () => result = LibraryDateFolderService.RenameRemark(sourcePath, "婚礼");

        action.Should().NotThrow();
        result.Should().NotBeNull();
        result!.Status.Should().Be(DateFolderRenameStatus.Failed);
        result.SourcePath.Should().Be(sourcePath);
        result.EffectivePath.Should().Be(sourcePath);
        Directory.Exists(source).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
