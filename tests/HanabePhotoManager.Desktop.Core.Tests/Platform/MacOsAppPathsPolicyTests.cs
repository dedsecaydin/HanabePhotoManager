using FluentAssertions;
using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Core.Tests.Platform;

public sealed class MacOsAppPathsPolicyTests
{
    [Fact]
    public void Resolve_UsesAppleApplicationSupportAndCaches()
    {
        var result = MacOsAppPathsPolicy.Resolve("/Users/hanabe");

        result.ApplicationDataDirectory.Should()
            .Be("/Users/hanabe/Library/Application Support/Hanabe Photo Manager");
        result.CacheDirectory.Should()
            .Be("/Users/hanabe/Library/Caches/Hanabe Photo Manager");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_RejectsMissingHome(string home)
    {
        var action = () => MacOsAppPathsPolicy.Resolve(home);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resolve_RejectsRelativeHome()
    {
        var action = () => MacOsAppPathsPolicy.Resolve("Users/hanabe");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resolve_NormalizesParentSegmentsWithinRoot()
    {
        var result = MacOsAppPathsPolicy.Resolve("/Users/hanabe/..");

        result.ApplicationDataDirectory.Should()
            .Be("/Users/Library/Application Support/Hanabe Photo Manager");
        result.CacheDirectory.Should()
            .Be("/Users/Library/Caches/Hanabe Photo Manager");
    }

    [Fact]
    public void Resolve_RejectsHomeContainingNullCharacter()
    {
        var action = () => MacOsAppPathsPolicy.Resolve("/Users/hanabe\0malicious");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resolve_PreservesRootHome()
    {
        var result = MacOsAppPathsPolicy.Resolve("/");

        result.ApplicationDataDirectory.Should()
            .Be("/Library/Application Support/Hanabe Photo Manager");
        result.CacheDirectory.Should()
            .Be("/Library/Caches/Hanabe Photo Manager");
    }

    [Fact]
    public void Resolve_ClampsParentSegmentsAtRoot()
    {
        var result = MacOsAppPathsPolicy.Resolve("/../Users");

        result.ApplicationDataDirectory.Should()
            .Be("/Users/Library/Application Support/Hanabe Photo Manager");
        result.CacheDirectory.Should()
            .Be("/Users/Library/Caches/Hanabe Photo Manager");
    }
}
