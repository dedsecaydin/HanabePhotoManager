using FluentAssertions;
using HanabePhotoManager.Desktop.Core.ViewModels;

namespace HanabePhotoManager.Desktop.Core.Tests.ViewModels;

public sealed class DesktopStartupCompositionTests
{
    [Fact]
    public void ValidateShell_CompletesForTheDesktopShell()
    {
        var action = DesktopStartupComposition.ValidateShell;

        action.Should().NotThrow();
    }
}
