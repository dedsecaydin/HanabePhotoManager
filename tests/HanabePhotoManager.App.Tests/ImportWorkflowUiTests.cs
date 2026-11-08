using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportWorkflowUiTests
{
    [Fact]
    public void ImportReview_HasSinglePrimaryActionAndSettingsOutOfTheWay()
    {
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        // 检查页只有一个主按钮；导入设置收进右上角弹出面板；全部素材默认收起。
        xaml.Should().Contain("Command=\"{Binding StartImportFromReviewCommand}\" AutomationProperties.Name=\"确认并开始导入\"");
        xaml.Should().Contain("IsOpen=\"{Binding IsImportSettingsOpen, Mode=TwoWay}\"");
        xaml.Should().Contain("Visibility=\"{Binding IsImportQueueExpanded, Converter={StaticResource BoolToVis}}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding ImportDateCards}\"");
    }

    [Fact]
    public void ImportTip_UsesStableContainerAndTextOnlyTransition()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        xaml.Should().Contain("x:Name=\"ImportTipCard\"").And.Contain("Height=\"72\"");
        xaml.Should().Contain("x:Name=\"ImportTipTextTransform\"");
        code.Should().Contain("AnimateImportTipText");
        code.Should().Contain("HandoffBehavior.SnapshotAndReplace");
    }

    [Fact]
    public void ImportDateFolderPreflight_ProvidesScrollableBatchEditingAndExplicitActions()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));
        var views = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Imports", "ImportDateFolderViews.xaml"));

        // 检查页的日期卡片直接编辑同一个 ImportDateFolderDecision（处理方式 / 已有文件夹 / 备注 / 最终文件夹）。
        xaml.Should().Contain("SelectedValue=\"{Binding Decision.Strategy}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding Decision.ExistingFolders}\"");
        xaml.Should().Contain("Text=\"{Binding Decision.Remark, UpdateSourceTrigger=PropertyChanged}\"");
        xaml.Should().Contain("Text=\"{Binding Decision.FinalDirectoryName}\"");
        views.Should().Contain("ItemsSource=\"{Binding ImportDateFolderDecisions}\"");
        views.Should().Contain("Text=\"{Binding DateText}\"");
        views.Should().Contain("Text=\"{Binding Remark, UpdateSourceTrigger=PropertyChanged}\"");
        views.Should().Contain("SelectedValue=\"{Binding Strategy}\"");
        views.Should().Contain("ItemsSource=\"{Binding ExistingFolders}\"");
        views.Should().Contain("Text=\"{Binding FinalDirectoryName}\"");
        views.Should().Contain("Style=\"{StaticResource Input.Switch}\"");
        views.Should().Contain("Content=\"{Binding SelectedImportDateGraphGroup.Decision}\" ContentTemplate=\"{StaticResource DateFolderEditor}\"");
        xaml.Should().Contain("Command=\"{Binding StartNewImportCommand}\"");
        xaml.Should().Contain("AutomationProperties.Name=\"确认并开始导入\"");
    }

    [Fact]
    public void ImportWorkflow_FreezesConfirmedDirectoryForDuplicateScanPlanAndResume()
    {
        var root = FindSourceRoot();
        var mainCode = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.Shared", "ViewModels", "MainWindowViewModel.cs"));
        var preflightCode = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.Shared", "ViewModels", "MainWindowViewModel.ImportDatePreflight.cs"));

        preflightCode.Should().Contain("ApplyRequiredRenames");
        // 确认后的目标按队列项冻结（同一天分段后同日期可去不同文件夹，HANABE-IMPORT-DAY-SPLIT）。
        preflightCode.Should().Contain("var targets = BuildImportTargetResolver();");
        preflightCode.Should().Contain("RunImportToTargetsAsync(selectedItems, deleteSources, targets)");
        // 查重范围以确认后的目标文件夹为主，再加同日其它备注文件夹。
        mainCode.Should().Contain("item.TargetDate is not null ? targetDirectoryFor(item) : null");
        mainCode.Should().Contain("BuildResumeEntry(item, group.Key.Date, group.Key.Directory)");
        mainCode.Should().Contain("targetDateDirectory).ConfigureAwait(true)");
    }

    [Fact]
    public void ImportResumePrompt_UsesThemedExplicitActionsAndNavigatesBeforeContinuing()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Imports", "ImportResumePromptWindow.xaml"));
        var mainCode = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        xaml.Should().Contain("Style=\"{DynamicResource Dialog.Window}\"");
        xaml.Should().Contain("Content=\"继续传输\"");
        xaml.Should().Contain("Content=\"放弃记录\"");
        xaml.Should().Contain("Content=\"稍后处理\"");
        xaml.Should().Contain("不会删除已导入文件或源文件");
        mainCode.Should().NotContain("检测到上次未完成的导入。是否继续？");
        mainCode.Should().Contain("_viewModel.ShowImportCommand.Execute(null)");
        mainCode.Should().Contain("await _viewModel.ResumePendingImportAsync()");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
