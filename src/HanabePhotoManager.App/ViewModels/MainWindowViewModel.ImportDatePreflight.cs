using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ImportDateFolderPreflightService _importDateFolderPreflightService = new();
    private bool _isImportDateFolderPreflightVisible;
    private bool _isImportDateGraphView;
    private ImportDateGraphGroupViewModel? _selectedImportDateGraphGroup;
    private string _preparedImportRoot = string.Empty;
    private string _preparedImportSource = string.Empty;
    private TransferMode _preparedTransferMode;
    private (ImportPreviewItemViewModel Item, LibraryDate? Date, CategoryChoice Category, string Session)[] _preparedImportItems = [];

    internal Func<bool> ConfirmImportMove { get; set; } = () =>
        new ImportMoveSafetyConfirmationWindow { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog() == true;

    internal Func<string, bool> ConfirmImportRenames { get; set; } = message =>
        new ImportMoveSafetyConfirmationWindow(message) { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog() == true;

    public string ImportWorkflowStage => IsBusy ? "正在处理 · 可安全停止"
        : IsImportDateFolderPreflightVisible ? "第 2 步 · 确认日期、备注与目标"
        : ImportSuccessCount + ImportSkippedCount + ImportFailedCount > 0 ? "第 3 步 · 查看结果与整理场次"
        : "第 1 步 · 选择来源并检查素材";

    /// <summary>导入四步向导当前所在步骤：1 选择来源 · 2 确认日期 · 3 导入 · 4 整理场次。</summary>
    public int ImportWizardStep => IsImportRunning ? 3
        : IsImportDateFolderPreflightVisible ? 2
        : ImportSuccessCount + ImportSkippedCount + ImportFailedCount > 0 ? 4
        : 1;

    private static readonly (string Title, string Hint)[] ImportWizardStepDefinitions =
    [
        ("选择来源", "存储卡或文件夹 · 检查素材"),
        ("确认日期", "日期文件夹、备注与目标"),
        ("导入", "可安全停止 · 保留进度"),
        ("整理场次", "查看结果 · 按时间分场次"),
    ];

    public IReadOnlyList<ImportWizardStepItem> ImportWizardSteps
    {
        get
        {
            var current = ImportWizardStep;
            return ImportWizardStepDefinitions
                .Select((step, index) => new ImportWizardStepItem(
                    index + 1,
                    step.Title,
                    step.Hint,
                    index + 1 < current ? "done" : index + 1 == current ? "active" : "todo"))
                .ToArray();
        }
    }

    public bool CanOrganizeImportedSessions => !IsBusy && LastImportedSessionPaths.Count > 0;

    public bool IsImportDateGraphView
    {
        get => _isImportDateGraphView;
        set => SetProperty(ref _isImportDateGraphView, value);
    }

    public IReadOnlyList<ImportDateGraphMonthViewModel> ImportDateGraphMonths { get; private set; } = [];

    public ImportDateGraphGroupViewModel? SelectedImportDateGraphGroup
    {
        get => _selectedImportDateGraphGroup;
        set
        {
            if (_selectedImportDateGraphGroup == value) return;
            if (_selectedImportDateGraphGroup is not null) _selectedImportDateGraphGroup.IsSelected = false;
            SetProperty(ref _selectedImportDateGraphGroup, value);
            if (value is not null) value.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectImportDateGraphGroup(ImportDateGraphGroupViewModel? group) => SelectedImportDateGraphGroup = group;

    public bool IsImportDateFolderPreflightVisible
    {
        get => _isImportDateFolderPreflightVisible;
        private set
        {
            if (SetProperty(ref _isImportDateFolderPreflightVisible, value))
            {
                ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
                BackFromImportDateFoldersCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(ImportWorkflowStage));
                OnPropertyChanged(nameof(ImportWizardSteps));
                OnPropertyChanged(nameof(CanEditImportOrganization));
            }
        }
    }

    public bool IsImportQueueVisible => !IsImportDateFolderPreflightVisible;

    private void PrepareImportDateFolderPreflight(IReadOnlyList<ImportPreviewItemViewModel> selectedItems)
    {
        _preparedImportRoot = LibraryRoot;
        _preparedImportSource = SourceFolder;
        _preparedTransferMode = SelectedTransferMode;
        _preparedImportItems = selectedItems.Select(item => (item, item.TargetDate, item.SelectedCategory, item.ImportSessionName)).ToArray();
        ImportDateFolderDecisions.Clear();
        var groups = selectedItems
            .Where(item => item.TargetDate is not null)
            .GroupBy(item => item.TargetDate!.Value)
            .Select(group => (group.Key, group.Count()));
        foreach (var decision in _importDateFolderPreflightService.CreateDecisions(LibraryRoot, groups))
        {
            decision.PropertyChanged += (_, _) => ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
            ImportDateFolderDecisions.Add(decision);
        }

        var mediaByDate = selectedItems.Where(item => item.TargetDate is not null).ToLookup(item => item.TargetDate!.Value);
        var graphGroups = ImportDateFolderDecisions
            .Select(decision => new ImportDateGraphGroupViewModel(decision, mediaByDate[decision.Date].ToArray()))
            .ToArray();
        ImportDateGraphMonths = graphGroups.GroupBy(group => (group.Decision.Date.Year, group.Decision.Date.Month))
            .Select(month => new ImportDateGraphMonthViewModel($"{month.Key.Year} 年 {month.Key.Month} 月", month.ToArray()))
            .ToArray();
        SelectedImportDateGraphGroup = graphGroups.FirstOrDefault();
        OnPropertyChanged(nameof(ImportDateGraphMonths));

        IsImportDateFolderPreflightVisible = ImportDateFolderDecisions.Count > 0;
        OnPropertyChanged(nameof(IsImportQueueVisible));
        ImportActionHint = "请批量确认每个日期的最终文件夹；确认前不会复制或移动任何媒体。";
        StatusMessage = "等待确认日期文件夹。";
        ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
    }

    private bool CanConfirmImportDateFolders() =>
        IsImportDateFolderPreflightVisible && !IsBusy &&
        ImportDateFolderDecisions.Count > 0 && ImportDateFolderDecisions.All(decision => decision.IsValid);

    private async Task ConfirmImportDateFoldersAsync()
    {
        if (!CanConfirmImportDateFolders()) return;
        if (_importResumeStore.HasPending)
        {
            ImportActionHint = "请先继续未完成导入，或放弃恢复记录后开始新任务。";
            RefreshImportResumeState();
            return;
        }
        var selectedItems = ImportItems.Where(item => item.IsSelected).ToArray();
        if (!string.Equals(LibraryRoot, _preparedImportRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(SourceFolder, _preparedImportSource, StringComparison.OrdinalIgnoreCase) ||
            SelectedTransferMode != _preparedTransferMode ||
            !_preparedImportItems.SequenceEqual(selectedItems.Select(item => (item, item.TargetDate, item.SelectedCategory, item.ImportSessionName))))
        {
            ClearImportDateFolderPreflight();
            ImportActionHint = "来源、图库、勾选、分类、日期或传输方式已变化，请重新预览日期与目标。";
            StatusMessage = "导入方案已变化，尚未修改文件。";
            return;
        }
        foreach (var decision in ImportDateFolderDecisions) _importDateFolderPreflightService.Refresh(decision);
        if (!CanConfirmImportDateFolders()) return;
        var deleteSources = SelectedTransferMode == TransferMode.MoveAfterVerify;
        if (deleteSources && !ConfirmImportMove()) return;
        var renames = ImportDateFolderDecisions.Where(decision => decision.Strategy == ImportDateFolderStrategy.RenameExisting &&
            !string.Equals(decision.SelectedExistingFolder?.FullPath, decision.FinalDirectoryPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (renames.Length > 0 && !ConfirmImportRenames("以下已有文件夹将被重命名；取消不会修改文件：\n\n" +
            string.Join("\n\n", renames.Select(decision => $"{decision.SelectedExistingFolder!.FullPath}\n→ {decision.FinalDirectoryPath}")))) return;
        if (!_importDateFolderPreflightService.ApplyRequiredRenames(ImportDateFolderDecisions))
        {
            ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
            StatusMessage = "日期文件夹确认失败，请检查行内提示。";
            return;
        }
        var targets = ImportDateFolderDecisions.ToDictionary(decision => decision.Date, decision => decision.FinalDirectoryPath);

        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
        await RunImportAsync(selectedItems, deleteSources, targets).ConfigureAwait(true);
    }

    private void BackFromImportDateFolders()
    {
        ClearImportDateFolderPreflight();
        ImportActionHint = "可调整来源、勾选或分类，然后重新进入日期文件夹确认。";
        StatusMessage = "已返回导入队列，尚未复制媒体。";
    }

    private void ClearImportDateFolderPreflight()
    {
        _preparedImportItems = [];
        SelectedImportDateGraphGroup = null;
        ImportDateGraphMonths = [];
        OnPropertyChanged(nameof(ImportDateGraphMonths));
        ImportDateFolderDecisions.Clear();
        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
    }
}
