using HanabePhotoManager.InstallerShell;
using Xunit;

namespace HanabePhotoManager.InstallerShell.Tests;

public sealed class InstallerFlowStateTests
{
    [Theory]
    [InlineData(0, 500, 500, true)]
    [InlineData(499, 500, 1000, true)]
    [InlineData(100, 500, 1000, false)]
    public void LicenseGateDetectsEnd(double offset, double viewport, double extent, bool expected)
        => Assert.Equal(expected, LicenseReadGate.HasReachedEnd(offset, viewport, extent));

    [Fact]
    public void LicenseRequiresReadingAndAcceptance()
    {
        var state = new InstallerFlowState();
        state.Continue();
        Assert.False(state.CanContinue);
        state.SetLicenseAccepted(true);
        Assert.False(state.CanContinue);
        state.MarkLicenseRead();
        state.SetLicenseAccepted(true);
        Assert.True(state.CanContinue);
    }

    [Fact]
    public void InstallingStepCannotGoBack()
    {
        var state = new InstallerFlowState();
        state.Continue();
        state.MarkLicenseRead();
        state.SetLicenseAccepted(true);
        state.Continue();
        state.Back();
        Assert.Equal(InstallerStep.Installing, state.Step);
    }
}
