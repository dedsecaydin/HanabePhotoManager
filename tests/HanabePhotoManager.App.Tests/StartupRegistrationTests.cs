using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class StartupRegistrationTests
{
    [Fact]
    public async Task FailedWrite_RollsBackUiState_AndReportsError()
    {
        var viewModel = new MainWindowViewModel(startupRegistrationService: new FailingStartupService());

        viewModel.LaunchAtStartup = true;
        for (var attempt = 0; attempt < 20 && viewModel.LaunchAtStartup; attempt++)
        {
            await Task.Delay(10);
        }

        viewModel.LaunchAtStartup.Should().BeFalse();
        viewModel.StatusMessage.Should().Contain("已回滚");
    }

    private sealed class FailingStartupService : IStartupRegistrationService
    {
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) => throw new UnauthorizedAccessException("denied");
    }
}
