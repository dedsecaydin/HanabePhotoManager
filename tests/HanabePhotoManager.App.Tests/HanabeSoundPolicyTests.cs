using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HanabeSoundPolicyTests
{
    [Theory]
    [InlineData(HanabeAssistantState.Failed, "error")]
    [InlineData(HanabeAssistantState.Interrupted, "error")]
    [InlineData(HanabeAssistantState.CompletedWithIssues, "error")]
    [InlineData(HanabeAssistantState.Resuming, "importing")]
    [InlineData(HanabeAssistantState.Canceled, "canceled")]
    public void RecoveryAndTerminalStates_ResolveExistingCueNames(HanabeAssistantState state, string cue)
    {
        HanabeSoundPolicy.Resolve(state, new(true, HanabeSoundStyle.Mixed, 35, false))
            .Should().EndWith($"/{cue}.wav");
    }

    [Fact]
    public void PerEventSwitchesAndQuietMode_AreBothRespected()
    {
        var disabled = new HanabeSoundSettings(true, HanabeSoundStyle.Mixed, 35, false,
            false, false, false, false, false, false);
        foreach (var kind in Enum.GetValues<HanabeSoundEvent>())
            HanabeSoundPolicy.Resolve(kind, disabled).Should().BeNull();
        HanabeSoundPolicy.Resolve(HanabeSoundEvent.Skipped, disabled with { SkipEnabled = true }).Should().EndWith("/skipped.wav");
        HanabeSoundPolicy.Resolve(HanabeSoundEvent.FeatureOpened, disabled with { OpenEnabled = true }).Should().EndWith("/opened.wav");
        HanabeSoundPolicy.Resolve(HanabeSoundEvent.Skipped, disabled with { SkipEnabled = true, QuietMode = true }).Should().BeNull();
        HanabeSoundPolicy.Resolve(HanabeAssistantState.Failed, disabled with { FailureEnabled = true, QuietMode = true }).Should().EndWith("/error.wav");
        HanabeSoundPolicy.Resolve(HanabeAssistantState.StopRequested, disabled with { CancelEnabled = true }).Should().BeNull();
        HanabeSoundPolicy.Resolve(HanabeAssistantState.Recoverable, disabled with { StartEnabled = true }).Should().BeNull();
    }
    [Theory]
    [InlineData(HanabeSoundStyle.Camera, HanabeAssistantState.Scanning, "Assets/Hanabe/Sounds/Camera/scanning.wav")]
    [InlineData(HanabeSoundStyle.Cute, HanabeAssistantState.Completed, "Assets/Hanabe/Sounds/Cute/completed.wav")]
    [InlineData(HanabeSoundStyle.Mixed, HanabeAssistantState.Error, "Assets/Hanabe/Sounds/Mixed/error.wav")]
    public void Resolve_ReturnsStyleAndStateAsset(HanabeSoundStyle style, HanabeAssistantState state, string expected)
    {
        HanabeSoundPolicy.Resolve(state, new(true, style, 35, false)).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, HanabeAssistantState.Completed, false)]
    [InlineData(true, HanabeAssistantState.Idle, false)]
    [InlineData(true, HanabeAssistantState.Scanning, true)]
    public void Resolve_RespectsEnabledAndIdle(bool enabled, HanabeAssistantState state, bool hasAsset)
    {
        var asset = HanabeSoundPolicy.Resolve(state, new(enabled, HanabeSoundStyle.Mixed, 35, false));
        if (hasAsset) asset.Should().NotBeNull(); else asset.Should().BeNull();
    }

    [Theory]
    [InlineData(HanabeAssistantState.Scanning, false)]
    [InlineData(HanabeAssistantState.Completed, true)]
    [InlineData(HanabeAssistantState.Error, true)]
    public void Resolve_QuietModeAllowsOnlyTerminalStates(HanabeAssistantState state, bool hasAsset)
    {
        var asset = HanabeSoundPolicy.Resolve(state, new(true, HanabeSoundStyle.Mixed, 35, true));
        if (hasAsset) asset.Should().NotBeNull(); else asset.Should().BeNull();
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(35, 0.35)]
    [InlineData(150, 1)]
    public void NormalizeVolume_ClampsToMediaPlayerRange(double input, double expected)
    {
        HanabeSoundPolicy.NormalizeVolume(input).Should().Be(expected);
    }
}
