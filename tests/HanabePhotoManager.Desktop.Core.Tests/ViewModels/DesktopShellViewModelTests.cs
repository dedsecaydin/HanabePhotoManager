using FluentAssertions;
using HanabePhotoManager.Desktop.Core.ViewModels;

namespace HanabePhotoManager.Desktop.Core.Tests.ViewModels;

public sealed class DesktopShellViewModelTests
{
    [Fact]
    public void Constructor_ExposesProductAndMigrationStatus()
    {
        var subject = new DesktopShellViewModel();

        subject.Title.Should().Be("Hanabe Photo Manager");
        subject.Status.Should().Be("macOS migration foundation");
    }
}
