using HanabePhotoManager.InstallerShell;
using Xunit;

namespace HanabePhotoManager.InstallerShell.Tests;

public sealed class InstallerExitCodeTests
{
    [Theory]
    [InlineData(0, InstallerOutcome.Success)]
    [InlineData(1602, InstallerOutcome.Cancelled)]
    [InlineData(1641, InstallerOutcome.RestartRequired)]
    [InlineData(3010, InstallerOutcome.RestartRequired)]
    [InlineData(87, InstallerOutcome.Failed)]
    public void ClassifiesMsiExitCodes(int exitCode, InstallerOutcome expected)
        => Assert.Equal(expected, InstallerExitCode.Classify(exitCode));
}
