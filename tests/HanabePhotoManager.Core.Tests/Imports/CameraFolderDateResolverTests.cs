using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class CameraFolderDateResolverTests
{
    private readonly CameraFolderDateResolver _resolver = new();

    [Fact]
    public void Resolve_UsesMajorityYearAndWarnsAboutMinorityYears()
    {
        var result = _resolver.Resolve("12060711", [2026, 2026, 2025]);

        result.Date.Should().Be(new LibraryDate(2026, 7, 11));
        result.RequiresConfirmation.Should().BeFalse();
        result.Warnings.Should().ContainSingle(message =>
            message.Contains("2025") && message.Contains("1"));
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("abc123")]
    public void Resolve_RequiresConfirmationWhenFolderHasNoFourDigitSequence(string folderName)
    {
        var result = _resolver.Resolve(folderName, [2026]);

        result.Date.Should().BeNull();
        result.RequiresConfirmation.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("camera1340", 2026)]
    [InlineData("camera0230", 2026)]
    public void Resolve_RequiresConfirmationForInvalidFolderDate(string folderName, int year)
    {
        var act = () => _resolver.Resolve(folderName, [year]);

        var result = act.Should().NotThrow().Subject;
        result.Date.Should().BeNull();
        result.RequiresConfirmation.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(2025, 2026)]
    [InlineData(2025, 2025, 2026, 2026)]
    public void Resolve_RequiresConfirmationWhenMostFrequentYearsAreTied(params int[] years)
    {
        var result = _resolver.Resolve("12060711", years);

        result.Date.Should().BeNull();
        result.RequiresConfirmation.Should().BeTrue();
        result.Warnings.Should().ContainSingle(message =>
            message.Contains("2025") && message.Contains("2026"));
    }

    [Theory]
    [MemberData(nameof(MissingValidYears))]
    public void Resolve_RequiresConfirmationWhenThereAreNoValidMetadataYears(int[] years)
    {
        var result = _resolver.Resolve("12060711", years);

        result.Date.Should().BeNull();
        result.RequiresConfirmation.Should().BeTrue();
        result.Warnings.Should().ContainSingle(message => message.Contains("年份"));
    }

    [Fact]
    public void Resolve_ReturnsFolderDateWithoutWarningForSingleValidYear()
    {
        var result = _resolver.Resolve("12060711", [2026]);

        result.Date.Should().Be(new LibraryDate(2026, 7, 11));
        result.RequiresConfirmation.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_AcceptsLeapDayForLeapYear()
    {
        var result = _resolver.Resolve("trip0229", [2024]);

        result.Date.Should().Be(new LibraryDate(2024, 2, 29));
        result.RequiresConfirmation.Should().BeFalse();
    }

    [Fact]
    public void Resolve_IgnoresInvalidYearsWhenChoosingTheMajority()
    {
        var result = _resolver.Resolve("12060711", [1899, 2026, 10000]);

        result.Date.Should().Be(new LibraryDate(2026, 7, 11));
        result.RequiresConfirmation.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    public static TheoryData<int[]> MissingValidYears => new()
    {
        Array.Empty<int>(),
        new[] { 0, 1899, 10000 }
    };
}
