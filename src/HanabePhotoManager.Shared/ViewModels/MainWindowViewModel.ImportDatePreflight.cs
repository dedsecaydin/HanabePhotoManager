using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.App.Platform;
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

    internal Func<bool> ConfirmImportMove { get; set; } = () => PlatformServices.ConfirmImportMove();

    internal Func<string, bool> ConfirmImportRenames { get; set; } = message => PlatformServices.ConfirmImportRenames(message);

    public string ImportWorkflowStage => IsBusy ? "正在处理 · 可安全停止"
        : IsImportDateFolderPreflightVisible ? "第 2 步 · 确认日期、备注与目标"
        : ImportSuccessCount + ImportSkippedCount + ImportFailedCount > 0 ? "第 3 步 · 查看结果与整理场次"
        : "第 1 步 · 选择来源并检查素材";

    /// <summary>导入三步：1 选择来源 · 2 检查确认（素材 + 日期与目标）· 3 导入与完成。完成后整理场次是结果页上的按钮。</summary>
    public int ImportWizardStep => ImportStage switch
    {
        "Running" or "Done" => 3,
        "Review" => 2,
        _ => 1,
    };

    private static readonly (string Title, string Hint)[] ImportWizardStepDefinitions =
    [
        ("选择来源", "存储卡或文件夹"),
        ("检查确认", "素材、日期与目标文件夹"),
        ("导入", "可安全停止 · 保留进度"),
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
                    index + 1 < current || (index + 1 == current && IsImportDoneStage) ? "done" : index + 1 == current ? "active" : "todo",
                    CanGoToImportWizardStep(index + 1)))
                .ToArray();
        }
    }

    /// <summary>步骤条上只有“回到选择来源”可以点（检查或完成后重新选来源）；不会删除任何文件。</summary>
    public bool CanGoToImportWizardStep(int step) => !IsBusy && step == 1 && (IsImportReviewStage || IsImportDoneStage);

    /// <summary>点击步骤条：回到第 1 步，清空本次队列重新选择来源。</summary>
    public bool GoToImportWizardStep(int step)
    {
        if (!CanGoToImportWizardStep(step)) return false;
        StartNewImportCommand.Execute(null);
        return true;
    }

    /// <summary>左栏主按钮上方的一句话：告诉用户现在在哪一步、按下去会发生什么。</summary>
    public string ImportNextStepHint =>
        IsBusy ? string.Empty
        : HasPendingImportResume ? "上次导入还没完成：继续后已完成的文件不会重复复制。"
        : ImportItems.Count == 0 ? "拖入或选择存储卡 / 文件夹后会自动分析，不会复制任何文件。"
        : $"已识别 {ImportItems.Count} 组素材。下一步确认日期文件夹，并在后台预先查重；确认前不会复制任何文件。";

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
        // 同一天有长时间间隔时按段生成多个目标文件夹决定（HANABE-IMPORT-DAY-SPLIT）。
        var dayPlans = BuildImportDayPlans(selectedItems);
        foreach (var decision in _importDateFolderPreflightService.CreateSegmentedDecisions(LibraryRoot, dayPlans))
        {
            decision.PropertyChanged += (_, _) =>
            {
                ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(ImportTargetFolderCount));
            };
            ImportDateFolderDecisions.Add(decision);
        }
        _importDayPlanDirty = false;
        OnPropertyChanged(nameof(ImportShootDayCount));
        OnPropertyChanged(nameof(ImportTargetFolderCount));

        var mediaByDate = selectedItems.Where(item => item.TargetDate is not null).ToLookup(item => item.TargetDate!.Value);
        var mediaByKey = selectedItems.GroupBy(item => item.PrimaryPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var graphGroups = ImportDateFolderDecisions
            .Select(decision => new ImportDateGraphGroupViewModel(decision, decision.MemberKeys.Count > 0
                ? decision.MemberKeys.Where(mediaByKey.ContainsKey).Select(key => mediaByKey[key]).ToArray()
                : mediaByDate[decision.Date].ToArray()))
            .ToArray();
        StartImportCaptureTimeEnrichment(selectedItems);
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
        if (IsImportDateFolderPreflightVisible) StartImportDuplicatePreview(selectedItems);
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
        var targets = BuildImportTargetResolver();

        // 正式导入会用最终文件夹重新查重（指纹缓存让这一步很快），后台预查到此为止。
        CancelImportDuplicatePreview();
        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
        await RunImportToTargetsAsync(selectedItems, deleteSources, targets).ConfigureAwait(true);
    }

    private void BackFromImportDateFolders()
    {
        ClearImportDateFolderPreflight();
        ImportActionHint = "可调整来源、勾选或分类，然后重新进入日期文件夹确认。";
        StatusMessage = "已返回导入队列，尚未复制媒体。";
    }

    private void ClearImportDateFolderPreflight()
    {
        CancelImportDuplicatePreview();
        ImportDuplicatePreviewText = string.Empty;
        _preparedImportItems = [];
        SelectedImportDateGraphGroup = null;
        ImportDateGraphMonths = [];
        OnPropertyChanged(nameof(ImportDateGraphMonths));
        ImportDateFolderDecisions.Clear();
        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
    }
}
