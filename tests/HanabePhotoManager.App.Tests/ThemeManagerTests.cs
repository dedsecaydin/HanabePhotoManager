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

    [Fact]
    public void ThemeManager_ExposesOneSharedThemeChangedEvent()
    {
        var eventInfo = typeof(ThemeManager).GetEvent("ThemeChanged");

        eventInfo.Should().NotBeNull();
        eventInfo!.EventHandlerType.Should().Be(typeof(EventHandler<AppTheme>));
    }

    [Theory]
    [InlineData(null, AppColorScheme.Dynamic)]
    [InlineData("", AppColorScheme.Dynamic)]
    [InlineData("unexpected", AppColorScheme.Dynamic)]
    [InlineData("forest", AppColorScheme.Forest)]
    [InlineData("FOREST", AppColorScheme.Forest)]
    [InlineData("violet", AppColorScheme.Violet)]
    public void ParseSchemePreference_UsesDynamicAsSafeDefault(string? value, AppColorScheme expected)
    {
        ThemeManager.ParseSchemePreference(value).Should().Be(expected);
    }
}
