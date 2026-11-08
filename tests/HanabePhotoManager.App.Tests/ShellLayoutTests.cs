using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>窗口外壳：侧栏收起 / 窄侧栏 / 按页面显示，以及导入页的步骤与结果摘要。</summary>
public sealed class ShellLayoutTests
{
    [Fact]
    public void Sidebar_ToggleCollapsesToZeroWidthAndBack()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SidebarColumnWidth.Should().Be(MainWindowViewModel.ExpandedSidebarWidth);

        viewModel.ToggleSidebarCommand.Execute(null);
        viewModel.IsSidebarCollapsed.Should().BeTrue();
        viewModel.IsSidebarVisible.Should().BeFalse();
        viewModel.SidebarColumnWidth.Should().Be(0);

        viewModel.ToggleSidebarCommand.Execute(null);
        viewModel.SidebarColumnWidth.Should().Be(MainWindowViewModel.ExpandedSidebarWidth);
    }

    [Fact]
    public void Sidebar_IconOnlyMode_UsesNarrowRail()
    {
        var viewModel = new MainWindowViewModel { NavigationDisplayMode = NavigationDisplayMode.Icon };

        viewModel.IsSidebarCompact.Should().BeTrue();
        viewModel.IsSidebarFull.Should().BeFalse();
        viewModel.SidebarColumnWidth.Should().Be(MainWindowViewModel.CompactSidebarWidth);
    }

    [Fact]
    public void HidingNavigationPage_KeepsOrder_ReturnsHomeAndCanBeRestored()
    {
        var viewModel = new MainWindowViewModel();
        var order = viewModel.NavigationItems.Select(item => item.Key).ToArray();
        viewModel.ShowImportCommand.Execute(null);

        viewModel.SetNavigationPageVisible("Import", false);

        viewModel.NavigationItems.Single(item => item.Key == "Import").IsVisible.Should().BeFalse();
        viewModel.HiddenNavigationKeys.Should().Contain("Import");
        viewModel.CurrentPage.Should().Be("Home", "隐藏当前所在页面后应回到主页，而不是停在看不见的页面");
        viewModel.NavigationItems.Select(item => item.Key).Should().Equal(order);

        viewModel.ShowAllNavigationPages();
        viewModel.NavigationItems.Should().OnlyContain(item => item.IsVisible);
        viewModel.HiddenNavigationKeys.Should().BeEmpty();
    }

    [Fact]
    public void HomePage_CannotBeHidden()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.SetNavigationPageVisible("Home", false);

        viewModel.NavigationItems.Single(item => item.Key == "Home").IsVisible.Should().BeTrue();
        viewModel.HiddenNavigationKeys.Should().BeEmpty();
    }

    [Fact]
    public void ImportReport_ShowsOneLineSummaryAndFoldsDetails()
    {
        var viewModel = new MainWindowViewModel
        {
            ImportReport = "导入完成：成功 2，跳过 0，失败 0" + Environment.NewLine + "完成：11.04 / C3498" + Environment.NewLine + "完成：11.04 / C3499",
        };

        viewModel.ImportReportHeadline.Should().Be("导入完成：成功 2，跳过 0，失败 0");
        viewModel.HasImportReportDetails.Should().BeTrue();
        viewModel.ImportReportDetailsHeader.Should().Be("详细记录（2 行）");
        viewModel.ImportReportDetails.Should().Contain("C3498").And.Contain("C3499").And.NotContain("导入完成");

        viewModel.ImportReport = "已停止分析。";
        viewModel.HasImportReportDetails.Should().BeFalse();
    }

    [Fact]
    public void ImportStepper_OnlyOffersStepsThatCanActuallyRun()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ImportWizardSteps.Should().OnlyContain(step => !step.IsClickable, "还没有分析任何素材");
        viewModel.GoToImportWizardStep(2).Should().BeFalse();
        viewModel.ImportNextStepHint.Should().Contain("自动分析");
    }

    [Fact]
    public void MainWindow_LayoutFillsWindowAndKeepsPrimaryActionPinned()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("x:Name=\"SidebarToggleButton\"").And.Contain("Command=\"{Binding ToggleSidebarCommand}\"");
        xaml.Should().Contain("ContextMenuOpening=\"Sidebar_ContextMenuOpening\"");
        xaml.Should().Contain("Visibility=\"{Binding IsVisible, Converter={StaticResource BoolToVis}}\"");
        // 导入页三步：不再有固定宽度的三栏；查重结果在检查页汇总卡片上。
        xaml.Should().NotContain("<ColumnDefinition Width=\"320\" />");
        xaml.Should().Contain("ToolTip=\"{Binding ImportDuplicatePreviewText}\"");
        xaml.Should().Contain("Visibility=\"{Binding IsImportSourceStage, Converter={StaticResource BoolToVis}}\"")
            .And.Contain("Visibility=\"{Binding IsImportReviewStage, Converter={StaticResource BoolToVis}}\"")
            .And.Contain("Visibility=\"{Binding IsImportRunningStage, Converter={StaticResource BoolToVis}}\"")
            .And.Contain("Visibility=\"{Binding IsImportDoneStage, Converter={StaticResource BoolToVis}}\"");
        xaml.Should().Contain("Text=\"{Binding ImportReportHeadline}\"");
        xaml.Should().Contain("MouseLeftButtonUp=\"ImportWizardStep_MouseLeftButtonUp\"");
        // 旧“归属确认”面板绑定的属性和命令在 VM 中都不存在（按钮无效），已移除；待确认项在队列卡片上直接改分类。
        xaml.Should().NotContain("ConfirmUnconfirmedImportCommand").And.NotContain("HasUnconfirmedItems");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
