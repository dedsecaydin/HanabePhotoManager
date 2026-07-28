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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
