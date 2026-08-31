using System.Drawing;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class WindowMaximizeBoundsPolicyTests
{
    [Fact]
    public void Calculate_UsesMonitorRelativePosition_ForNegativeCoordinateSecondaryScreen()
    {
        var monitor = new Rectangle(1920, -1080, 1920, 1080);
        var workArea = new Rectangle(1920, -1040, 1920, 1040);

        var result = WindowMaximizeBoundsPolicy.Calculate(monitor, workArea);

        result.Should().Be(new Rectangle(0, 40, 1920, 1040));
    }
}
