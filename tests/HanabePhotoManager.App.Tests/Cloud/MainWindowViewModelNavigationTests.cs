using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests.Cloud;

public sealed class MainWindowViewModelNavigationTests
{
    [Fact]
    public void LegacyBaiduCommand_ForwardsToUnifiedCloudPage()
    {
        var viewModel = new MainWindowViewModel();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        viewModel.ShowBaiduCloudCommand.Execute(null);

        viewModel.CurrentPage.Should().Be("Cloud");
        viewModel.SelectedCloudProvider.Should().Be(CloudProviderChoice.Baidu);
        viewModel.IsCloudPage.Should().BeTrue();
        viewModel.IsBaiduCloudPage.Should().BeTrue();
        viewModel.PageTitle.Should().Be("网盘");
        viewModel.PageSubtitle.Should().Contain("切换");
        notifications.Should().Contain(nameof(MainWindowViewModel.CurrentPage));
        notifications.Should().Contain(nameof(MainWindowViewModel.IsCloudPage));
        notifications.Should().Contain(nameof(MainWindowViewModel.SelectedCloudProvider));
    }

    [Fact]
    public void LegacyQuarkCommand_ForwardsToUnifiedCloudPage()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ShowQuarkCloudCommand.Execute(null);

        viewModel.CurrentPage.Should().Be("Cloud");
        viewModel.SelectedCloudProvider.Should().Be(CloudProviderChoice.Quark);
        viewModel.IsQuarkCloudSelected.Should().BeTrue();
        viewModel.IsQuarkCloudPage.Should().BeTrue();
    }

    [Fact]
    public void DefaultNavigation_ContainsOnlyOneUnifiedCloudEntry()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.NavigationItems.Select(item => item.Key)
            .Should().ContainSingle(key => key == "Cloud");
        viewModel.NavigationItems.Select(item => item.Key)
            .Should().NotContain(["BaiduCloud", "QuarkCloud"]);
    }
}
