using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class HanabeAssistantPresentationPolicyTests
{
    [Fact]
    public void ScanningWithUnknownTotal_UsesIndeterminateProgress()
    {
        var snapshot = HanabeAssistantPresentationPolicy.Create(HanabeAssistantState.Scanning, "已发现 12 个文件", 0, true, 0, 0, 0);
        snapshot.IsProgressVisible.Should().BeTrue();
        snapshot.IsProgressIndeterminate.Should().BeTrue();
        snapshot.TaskTitle.Should().Be("正在扫描媒体");
    }

    [Fact]
    public void StopRequested_IsNeitherCancelableAgainNorAnError()
    {
        var snapshot = HanabeAssistantPresentationPolicy.Create(HanabeAssistantState.StopRequested, "已请求停止", 42, false, 10, 1, 0, true);
        snapshot.CanStop.Should().BeFalse();
        snapshot.StatusLabel.Should().Be("正在停止");
        snapshot.TaskTitle.Should().Contain("安全停止");
    }

    [Theory]
    [InlineData(HanabeAssistantState.Canceled, true, false, true)]
    [InlineData(HanabeAssistantState.Interrupted, true, true, true)]
    [InlineData(HanabeAssistantState.CompletedWithIssues, false, true, true)]
    [InlineData(HanabeAssistantState.Completed, false, true, false)]
    public void TerminalPolicy_ExposesOnlyValidActions(HanabeAssistantState state, bool resume, bool report, bool persistent)
    {
        var snapshot = HanabeAssistantPresentationPolicy.Create(state, "详情", 80, false, 7, 2, 1, true);
        snapshot.CanResume.Should().Be(resume);
        snapshot.CanViewReport.Should().Be(report);
        snapshot.IsPersistent.Should().Be(persistent);
    }

    [Fact]
    public void AnimationResolver_MapsNewStatesToExistingAssets()
    {
        HanabeAssistantAnimationResolver.Resolve(HanabeAssistantVisualStyle.ChibiAnimated, HanabeAssistantState.StopRequested)
            .Should().EndWith("/idle.gif");
        HanabeAssistantAnimationResolver.Resolve(HanabeAssistantVisualStyle.PixelAnimated, HanabeAssistantState.Interrupted)
            .Should().EndWith("/error.gif");
    }
}
