using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests.Cloud;

public sealed class MainWindowViewModelNavigationTests
{
    [Fact]
    public void ShowCloudCommand_ChangesPageAndCloudPresentation()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ShowBaiduCloudCommand.Execute(null);

        viewModel.CurrentPage.Should().Be("BaiduCloud");
        viewModel.IsBaiduCloudPage.Should().BeTrue();
        viewModel.IsHomePage.Should().BeFalse();
        viewModel.PageTitle.Should().Be("百度网盘");
        viewModel.PageSubtitle.Should().Contain("内嵌浏览器");
    }

    [Fact]
    public void ShowCloudCommand_NotifiesAllCloudNavigationProperties()
    {
        var viewModel = new MainWindowViewModel();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        viewModel.ShowBaiduCloudCommand.Execute(null);

        notifications.Should().Contain(nameof(MainWindowViewModel.CurrentPage));
        notifications.Should().Contain(nameof(MainWindowViewModel.IsBaiduCloudPage));
        notifications.Should().Contain(nameof(MainWindowViewModel.PageTitle));
        notifications.Should().Contain(nameof(MainWindowViewModel.PageSubtitle));
    }
}
