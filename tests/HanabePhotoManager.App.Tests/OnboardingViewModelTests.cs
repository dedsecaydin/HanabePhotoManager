using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class OnboardingViewModelTests
{
    [Fact]
    public void ReplayAndNavigation_GuidesMainFeaturesThenOffersOptionalExtendedTour()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ReplayOnboardingCommand.Execute(null);

        viewModel.IsOnboardingVisible.Should().BeTrue();
        viewModel.OnboardingStep.Should().Be(0);
        viewModel.OnboardingStepCount.Should().BeGreaterThan(10);
        viewModel.IsOnboardingLibraryStep.Should().BeTrue();
        viewModel.OnboardingTitle.Should().Contain("图库根目录");

        viewModel.NextOnboardingStepCommand.Execute(null);
        viewModel.IsOnboardingSourceStep.Should().BeTrue();
        viewModel.CurrentPage.Should().Be("Import");

        viewModel.NextOnboardingStepCommand.Execute(null);
        viewModel.IsOnboardingImportStep.Should().BeTrue();

        while (!viewModel.IsOnboardingContinuationChoiceStep)
            viewModel.NextOnboardingStepCommand.Execute(null);

        viewModel.OnboardingTitle.Should().Contain("继续");
        viewModel.ShowStandardOnboardingNavigation.Should().BeFalse();

        viewModel.ContinueOnboardingCommand.Execute(null);
        viewModel.ShowStandardOnboardingNavigation.Should().BeTrue();
        viewModel.CurrentPage.Should().Be("Compression");
    }
}
