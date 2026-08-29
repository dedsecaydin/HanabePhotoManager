using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportWorkflowUiTests
{
    [Fact]
    public void ImportPrimaryAction_HasStandardSpacingAfterAdvancedOptions()
    {
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("Content=\"开始分析与导入\" Style=\"{StaticResource Button.Primary}\" Margin=\"0,12,0,0\"");
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
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("Text=\"确认日期文件夹\"");
        xaml.Should().Contain("ItemsSource=\"{Binding ImportDateFolderDecisions}\"");
        xaml.Should().Contain("Text=\"{Binding DateText}\"");
        xaml.Should().Contain("Text=\"{Binding Remark, UpdateSourceTrigger=PropertyChanged}\"");
        xaml.Should().Contain("SelectedValue=\"{Binding Strategy}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding ExistingFolders}\"");
        xaml.Should().Contain("Text=\"{Binding FinalDirectoryName}\"");
        xaml.Should().Contain("Command=\"{Binding BackFromImportDateFoldersCommand}\"");
        xaml.Should().Contain("Command=\"{Binding ConfirmImportDateFoldersCommand}\"");
        xaml.Should().Contain("AutomationProperties.Name=\"确认并开始导入\"");
    }

    [Fact]
    public void ImportWorkflow_FreezesConfirmedDirectoryForDuplicateScanPlanAndResume()
    {
        var root = FindSourceRoot();
        var mainCode = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));
        var preflightCode = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.ImportDatePreflight.cs"));

        preflightCode.Should().Contain("ApplyRequiredRenames");
        preflightCode.Should().Contain("ToDictionary(decision => decision.Date, decision => decision.FinalDirectoryPath)");
        mainCode.Should().Contain("dateDirectories[date]");
        mainCode.Should().Contain("BuildResumeEntry(item, group.Key, dateDirectories[group.Key])");
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
