using System.IO;
using System.Reflection;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportDateDualViewTests : IDisposable
{
    private readonly string _root = Path.Combine(@"D:\HanabePhoto\.artifacts\dual-view-tests", Guid.NewGuid().ToString("N"));

    public ImportDateDualViewTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void SwitchAndGraphEdits_PreserveSameDecisionAndConfirmation()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var items = Enumerable.Range(1, 5).Select(i => CreateItem(vm, new LibraryDate(2026, 10, 1), i)).ToArray();
        Prepare(vm, items);
        var decision = Assert.Single(vm.ImportDateFolderDecisions);
        var group = Assert.Single(Assert.Single(vm.ImportDateGraphMonths).Groups);

        Assert.False(vm.IsImportDateGraphView);
        Assert.Same(decision, group.Decision);
        Assert.Equal(2, group.Samples.Count);
        Assert.Equal("+3", group.OverflowText);
        Assert.Equal(items, group.Items);
        Assert.True(vm.ConfirmImportDateFoldersCommand.CanExecute(null));

        decision.Remark = "客户A";
        var target = decision.FinalDirectoryPath;
        vm.IsImportDateGraphView = true;
        Assert.Equal(target, group.Decision.FinalDirectoryPath);
        group.Decision.Remark = "客户B";
        vm.IsImportDateGraphView = false;
        Assert.Equal("客户B", decision.Remark);
        Assert.Contains("客户B", decision.FinalDirectoryPath);
        Assert.True(vm.ConfirmImportDateFoldersCommand.CanExecute(null));

        group.Decision.Strategy = ImportDateFolderStrategy.UseExisting;
        Assert.False(vm.ConfirmImportDateFoldersCommand.CanExecute(null));
        Assert.False(decision.IsValid);
        Assert.NotEmpty(decision.ValidationMessage);
    }

    [Fact]
    public void MonthsAndBackNavigation_UseOnlyPreparedMediaAndClearStaleGraph()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var first = CreateItem(vm, new LibraryDate(2026, 10, 1), 1);
        var second = CreateItem(vm, new LibraryDate(2026, 11, 2), 2);
        Prepare(vm, [first, second]);
        Assert.Equal(2, vm.ImportDateGraphMonths.Count);
        var group = vm.ImportDateGraphMonths[1].Groups.Single();
        vm.SelectImportDateGraphGroupCommand.Execute(group);
        Assert.Same(group, vm.SelectedImportDateGraphGroup);
        Assert.True(group.IsSelected);
        Assert.False(vm.ImportDateGraphMonths[0].Groups[0].IsSelected);
        Assert.False(group.HasOverflow);

        vm.BackFromImportDateFoldersCommand.Execute(null);
        Assert.Empty(vm.ImportDateFolderDecisions);
        Assert.Empty(vm.ImportDateGraphMonths);
        Assert.Null(vm.SelectedImportDateGraphGroup);
        Assert.True(vm.IsImportQueueVisible);
        Prepare(vm, [first]);
        Assert.Same(first, Assert.Single(vm.SelectedImportDateGraphGroup!.Items));
    }

    private static ImportPreviewItemViewModel CreateItem(MainWindowViewModel vm, LibraryDate date, int index)
    {
        var file = new SourceMediaFile($@"D:\sample\C{index:0000}.MP4", 10, DateTimeOffset.UtcNow);
        return new ImportPreviewItemViewModel(new MediaGroup($"C{index:0000}", MediaCategory.Video, file, []),
            MediaCategory.Video, vm.CategoryChoices, false, date, null, string.Empty, index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelDangerousConfirmation_DoesNotRenameExistingFolder(bool graph)
    {
        var oldPath = Path.Combine(_root, "10月", "10.01_原备注");
        Directory.CreateDirectory(oldPath);
        File.WriteAllText(Path.Combine(oldPath, "keep.txt"), "original");
        var vm = new MainWindowViewModel { LibraryRoot = _root, SelectedTransferMode = TransferMode.MoveAfterVerify };
        var item = CreateItem(vm, new LibraryDate(2026, 10, 1), 1);
        item.IsSelected = true;
        vm.ImportItems.Add(item);
        Prepare(vm, [item]);
        vm.IsImportDateGraphView = graph;
        var decision = Assert.Single(vm.ImportDateFolderDecisions);
        decision.Strategy = ImportDateFolderStrategy.RenameExisting;
        decision.Remark = "新备注";
        var confirmations = 0;
        vm.ConfirmImportMove = () => { confirmations++; return false; };
        await vm.ConfirmImportDateFoldersCommand.ExecuteAsync(null);
        Assert.Equal(1, confirmations);
        Assert.True(Directory.Exists(oldPath));
        Assert.False(Directory.Exists(decision.FinalDirectoryPath));
        Assert.Equal("original", File.ReadAllText(Path.Combine(oldPath, "keep.txt")));
        vm.ConfirmImportMove = () => true;
        vm.ConfirmImportRenames = _ => false;
        await vm.ConfirmImportDateFoldersCommand.ExecuteAsync(null);
        Assert.True(Directory.Exists(oldPath));
        Assert.True(vm.IsImportDateFolderPreflightVisible);
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("source")]
    [InlineData("root")]
    [InlineData("mode")]
    public async Task ChangedPlan_RequiresNewPreviewBeforeAnyConfirmation(string change)
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root };
        var item = CreateItem(vm, new LibraryDate(2026, 10, 1), 1);
        item.IsSelected = true;
        vm.ImportItems.Add(item);
        Prepare(vm, [item]);
        if (change == "selection") item.IsSelected = false;
        if (change == "source") vm.SourceFolder = _root;
        if (change == "root") vm.LibraryRoot = Path.Combine(_root, "other");
        if (change == "mode") vm.SelectedTransferMode = TransferMode.MoveAfterVerify;
        vm.ConfirmImportMove = () => throw new InvalidOperationException("Stale plan reached confirmation");
        await vm.ConfirmImportDateFoldersCommand.ExecuteAsync(null);
        Assert.False(vm.IsImportDateFolderPreflightVisible);
        Assert.Empty(vm.ImportDateGraphMonths);
        Assert.Contains("重新预览", vm.ImportActionHint);
    }

    [Fact]
    public async Task Analysis_FirstClickStopsAtReview_SecondClickPreparesSharedPlan()
    {
        var source = Path.Combine(_root, "source", "10.01");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "C0001.MP4"), [1, 2, 3]);
        var library = Path.Combine(_root, "library");
        Directory.CreateDirectory(library);
        var vm = new MainWindowViewModel { LibraryRoot = library, SourceFolder = source };
        await vm.AnalyzeAndImportCommand.ExecuteAsync(null);
        Assert.Single(vm.ImportItems);
        Assert.False(vm.IsImportDateFolderPreflightVisible);
        Assert.Contains("下一步", vm.ImportPrimaryActionText);
        await vm.AnalyzeAndImportCommand.ExecuteAsync(null);
        Assert.True(vm.IsImportDateFolderPreflightVisible);
        Assert.Empty(Directory.GetFileSystemEntries(library));
    }

    [Fact]
    public async Task CancelAnalysis_PreservesStoppedMessageAndDoesNotContinue()
    {
        var vm = new MainWindowViewModel { LibraryRoot = _root, SourceFolder = _root };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.ProgressLabel) && vm.ProgressLabel == "正在读取来源文件夹…")
                vm.CancelCurrentTaskCommand.Execute(null);
        };
        await vm.AnalyzeAndImportCommand.ExecuteAsync(null);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.ImportItems);
        Assert.False(vm.IsImportDateFolderPreflightVisible);
        Assert.Equal("已停止", vm.ProgressLabel);
        Assert.Contains("已停止", vm.ImportActionHint);
    }

    [Fact]
    public void ChangingBusyState_DoesNotOverwriteActionableMessage()
    {
        var vm = new MainWindowViewModel { ImportActionHint = "失败原因与重试建议" };
        vm.IsBusy = true;
        vm.IsBusy = false;
        Assert.Equal("失败原因与重试建议", vm.ImportActionHint);
    }

    private static void Prepare(MainWindowViewModel vm, IReadOnlyList<ImportPreviewItemViewModel> items) =>
        typeof(MainWindowViewModel).GetMethod("PrepareImportDateFolderPreflight", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vm, [items]);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
