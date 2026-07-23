using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ThemeManagerTests
{
    [Theory]
    [InlineData(null, AppTheme.Light)]
    [InlineData("", AppTheme.Light)]
    [InlineData("unexpected", AppTheme.Light)]
    [InlineData("dark", AppTheme.Dark)]
    [InlineData("LIGHT", AppTheme.Light)]
    public void ParsePreference_UsesLightAsSafeDefault(string? value, AppTheme expected)
    {
        ThemeManager.ParsePreference(value).Should().Be(expected);
    }
}
