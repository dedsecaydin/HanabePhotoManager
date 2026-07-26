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
}
