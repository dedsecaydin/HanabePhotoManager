using FluentAssertions;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class ImportNamingFormatterTests
{
    [Theory]
    [InlineData("JK{seq}", 1, "JK0001")]
    [InlineData("JK{seq}", 122, "JK0122")]
    [InlineData("JK{seq:6}", 7, "JK000007")]
    [InlineData("{orig}", 5, "photo")]
    [InlineData("{date}_{seq}", 3, "20260815_0003")]
    public void Format_ExpandsPlaceholders(string template, int sequence, string expected)
    {
        ImportNamingFormatter.Format(template, sequence, "photo", new LibraryDate(2026, 8, 15))
            .Should().Be(expected);
    }

    [Fact]
    public void Format_FallsBackToDefaultWhenTemplateIsBlank()
    {
        ImportNamingFormatter.Format(null, 1, "photo", new LibraryDate(2026, 8, 15))
            .Should().Be("JK0001");
        ImportNamingFormatter.Format("  ", 2, "photo", new LibraryDate(2026, 8, 15))
            .Should().Be("JK0002");
    }

    [Theory]
    [InlineData("{orig}", true)]
    [InlineData("photo_{orig}", true)]
    [InlineData("JK{seq}", false)]
    [InlineData(null, false)]
    public void UsesOriginalName_DetectsPlaceholder(string? template, bool expected)
    {
        ImportNamingFormatter.UsesOriginalName(template).Should().Be(expected);
    }
}
