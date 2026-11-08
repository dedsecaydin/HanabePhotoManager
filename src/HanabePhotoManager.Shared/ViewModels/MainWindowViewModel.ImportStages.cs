using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Platform;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 导入页三步：选择来源 → 检查确认（素材 + 日期与目标在同一屏）→ 导入与完成。
/// 页面每一刻只显示当前这一步需要的东西；底层仍是原来的分析 / 日期方案 / 查重 / 续传逻辑。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private bool _isImportSettingsOpen;
    private bool _isImportOrganizePanelOpen;
    private bool _inlinePlanRefreshPending;

    /// <summary>Source（选来源）/ Analyzing（正在分析）/ Review（检查确认）/ Running（导入中）/ Done（完成）。</summary>
    public string ImportStage => IsImportRunning ? "Running"
        : IsBusy && _activeTaskKind == ActiveTaskKind.Analysis ? "Analyzing"
        : ImportSuccessCount + ImportSkippedCount + ImportFailedCount > 0 ? "Done"
        : ImportItems.Count > 0 ? "Review"
        : "Source";

    public bool IsImportSourceStage => ImportStage is "Source" or "Analyzing";
    public bool IsImportAnalyzing => ImportStage == "Analyzing";
    public bool IsImportReviewStage => ImportStage == "Review";
    public bool IsImportRunningStage => ImportStage == "Running";
    public bool IsImportDoneStage => ImportStage == "Done";

    /// <summary>右上角“导入设置”弹出面板（转移方式、命名、查重、人物识别）。</summary>
    public bool IsImportSettingsOpen { get => _isImportSettingsOpen; set => SetProperty(ref _isImportSettingsOpen, value); }

    /// <summary>检查页右侧“分类与场次”面板，默认收起；需要整理时再打开，拖拽缩略图到分类 / 场次。</summary>
    public bool IsImportOrganizePanelOpen { get => _isImportOrganizePanelOpen; set => SetProperty(ref _isImportOrganizePanelOpen, value); }

    public bool HasImportDevices => ImportDeviceChoices.Count > 0;

    private bool _isImportQueueExpanded;

    /// <summary>检查页默认只显示按日期汇总的图形卡片；需要逐张查看或调整时再展开全部素材。</summary>
    public bool IsImportQueueExpanded
    {
        get => _isImportQueueExpanded;
        set
        {
            if (SetProperty(ref _isImportQueueExpanded, value))
            {
                OnPropertyChanged(nameof(ImportQueueToggleText));
                if (!value) IsImportOrganizePanelOpen = false;
            }
        }
    }

    public string ImportQueueToggleText => IsImportQueueExpanded
        ? "收起全部素材"
        : $"查看全部素材（{ImportItems.Count:N0} 组）";

    /// <summary>检查页的日期卡片（每个日期一张：缩略图条、分类比例、目标文件夹）。</summary>
    public IReadOnlyList<ImportDateGraphGroupViewModel> ImportDateCards =>
        ImportDateGraphMonths.SelectMany(month => month.Groups).ToArray();

    /// <summary>读不到日期或分类的数量：检查页顶部提示，点一下展开全部素材去处理。</summary>
    public int ImportUnresolvedCount => ImportItems.Count(item => item.IsSelected && item.NeedsAttention);

    public bool HasImportUnresolved => ImportUnresolvedCount > 0;

    public string ImportReviewTotalSize
    {
        get
        {
            var bytes = ImportItems.Where(item => item.IsSelected).Sum(item => item.TotalBytes);
            return bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.##} GB" : $"{bytes / (double)(1L << 20):0.#} MB";
        }
    }

    public int ImportSelectedCount => ImportItems.Count(item => item.IsSelected);

    public string ImportDuplicateCountText => _importDuplicatePreviewCounts is null ? "查重中" : $"{_importDuplicatePreviewTotal:N0}";

    /// <summary>检查页底栏的一句话：多少组、多少日期、重复怎么处理。</summary>
    public string ImportReviewSummary
    {
        get
        {
            var selected = ImportItems.Count(item => item.IsSelected);
            var dates = ImportShootDayCount;
            var parts = new List<string> { $"已勾选 {selected:N0} / {ImportItems.Count:N0} 组" };
            if (dates > 0) parts.Add($"{dates} 个日期");
            if (_importDuplicatePreviewCounts is not null && _importDuplicatePreviewTotal > 0)
                parts.Add($"{_importDuplicatePreviewTotal} 个重复将{ImportDuplicateHandlingVerb}");
            return string.Join(" · ", parts);
        }
    }

    public bool CanStartImportFromReview => IsImportReviewStage && (CanConfirmImportDateFolders() || !IsImportDateFolderPreflightVisible);

    private IAsyncRelayCommand? _startImportFromReviewCommand;

    /// <summary>
    /// 检查页的主按钮“开始导入”：日期方案已就绪则直接确认并开始；还没有方案（或方案已过期）时先生成方案、停在页面上让用户看一眼。
    /// </summary>
    public IAsyncRelayCommand StartImportFromReviewCommand => _startImportFromReviewCommand ??= new AsyncRelayCommand(async () =>
    {
        if (HasPendingImportResume) { await ResumePendingImportAsync().ConfigureAwait(true); return; }
        if (!IsImportDateFolderPreflightVisible || !IsInlinePlanCurrent())
        {
            await ImportSelectedAsync().ConfigureAwait(true);
            NotifyImportStageChanged();
            return;
        }
        if (!CanConfirmImportDateFolders())
        {
            ImportActionHint = "有日期的目标文件夹还需要调整（看该行下方的黄色提示），改好后再开始导入。";
            return;
        }
        await ConfirmImportDateFoldersCommand.ExecuteAsync(null).ConfigureAwait(true);
        NotifyImportStageChanged();
    });

    private IRelayCommand? _startNewImportCommand;

    /// <summary>完成页的“继续导入其它素材”：清空本次队列与结果，回到选择来源。不删除任何文件。</summary>
    public IRelayCommand StartNewImportCommand => _startNewImportCommand ??= new RelayCommand(() =>
    {
        if (IsBusy) return;
        IsImportQueueExpanded = false;
        ClearImportDateFolderPreflight();
        CancelImportThumbnailLoading();
        ImportItems.Clear();
        ImportSections.Clear();
        ImportSourceCards.Clear();
        SetImportSourcePaths([]);
        SetImportSummary(0, 0, 0);
        ImportReport = string.Empty;
        ImportActionHint = "插入存储卡或选择文件夹开始新的导入。";
        RefreshImportDevices();
        NotifyImportStageChanged();
    });

    private IAsyncRelayCommand<string>? _importFromDevicePathCommand;

    /// <summary>选来源页的设备卡片“导入”：只分析这一张卡（只读）。</summary>
    public IAsyncRelayCommand<string> ImportFromDevicePathCommand => _importFromDevicePathCommand ??= new AsyncRelayCommand<string>(async path =>
    {
        if (string.IsNullOrWhiteSpace(path) || IsBusy) return;
        await PrepareMultipleImportSourcesAsync([path]).ConfigureAwait(true);
        NotifyImportStageChanged();
    });

    private void NotifyImportStageChanged()
    {
        OnPropertyChanged(nameof(ImportStage));
        OnPropertyChanged(nameof(IsImportSourceStage));
        OnPropertyChanged(nameof(IsImportAnalyzing));
        OnPropertyChanged(nameof(IsImportReviewStage));
        OnPropertyChanged(nameof(IsImportRunningStage));
        OnPropertyChanged(nameof(IsImportDoneStage));
        OnPropertyChanged(nameof(HasImportDevices));
        OnPropertyChanged(nameof(ImportReviewSummary));
        OnPropertyChanged(nameof(ImportDateCards));
        OnPropertyChanged(nameof(ImportUnresolvedCount));
        OnPropertyChanged(nameof(HasImportUnresolved));
        OnPropertyChanged(nameof(ImportReviewTotalSize));
        OnPropertyChanged(nameof(ImportSelectedCount));
        OnPropertyChanged(nameof(ImportDuplicateCountText));
        OnPropertyChanged(nameof(ImportQueueToggleText));
        OnPropertyChanged(nameof(CanStartImportFromReview));
        OnPropertyChanged(nameof(ImportWizardSteps));
    }

    // ---------- 检查页：日期方案随队列自动更新 ----------

    /// <summary>
    /// 界面在队列变化（勾选、分类、日期、场次、增删）后调用：合并到一次后台刷新。
    /// 只在有界面调度器时生效；没有界面时（自动化测试）保持原来“手动生成方案”的行为。
    /// </summary>
    public void ScheduleInlineDatePlanRefresh()
    {
        var dispatcher = PlatformServices.Dispatcher;
        if (dispatcher is InlineUiDispatcher || _inlinePlanRefreshPending) return;
        _inlinePlanRefreshPending = true;
        dispatcher.Post(() =>
        {
            _inlinePlanRefreshPending = false;
            RefreshInlineDatePlan();
        }, background: true);
    }

    private bool IsInlinePlanCurrent()
    {
        var selected = ImportItems.Where(item => item.IsSelected).ToArray();
        return string.Equals(LibraryRoot, _preparedImportRoot, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SourceFolder, _preparedImportSource, StringComparison.OrdinalIgnoreCase)
            && SelectedTransferMode == _preparedTransferMode
            && _preparedImportItems.SequenceEqual(selected.Select(item => (item, item.TargetDate, item.SelectedCategory, item.ImportSessionName)));
    }

    /// <summary>重新生成日期与目标方案，保留用户已为每个日期选好的处理方式、已有文件夹和备注。</summary>
    internal void RefreshInlineDatePlan()
    {
        if (IsBusy || HasPendingImportResume || ImportItems.Count == 0 || !IsImportReviewStage)
        {
            NotifyImportStageChanged();
            return;
        }

        if (IsImportDateFolderPreflightVisible && IsInlinePlanCurrent() && !_importDayPlanDirty)
        {
            NotifyImportStageChanged();
            return;
        }

        var unresolved = ImportItems.Where(item => item.IsSelected &&
            (item.SelectedCategory.Category == MediaCategory.Unconfirmed || item.TargetDate is null)).ToArray();
        var selected = ImportItems.Where(item => item.IsSelected)
            .Where(item => item.SelectedCategory.Category != MediaCategory.Unconfirmed)
            .Where(item => ConcreteCategoryFolders.ContainsKey(item.SelectedCategory.Category))
            .ToArray();

        if (!HasLibraryRoot || unresolved.Length > 0 || selected.Length == 0)
        {
            ClearImportDateFolderPreflight();
            ImportActionHint = !HasLibraryRoot ? "还没有选择照片库，开始导入时会先请你选一个。"
                : unresolved.Length > 0 ? $"还有 {unresolved.Length} 项读不到日期或无法判断分类：在下方卡片上改一下分类，或取消勾选。"
                : "勾选要导入的素材。";
            NotifyImportStageChanged();
            return;
        }

        // 按（日期, 第几段, 共几段）记住用户的选择；分段方式变了的日期回到新的默认值。
        var previous = ImportDateFolderDecisions.ToDictionary(
            decision => (decision.Date, decision.SegmentIndex, decision.SegmentCount),
            decision => (decision.Strategy, decision.Remark, Existing: decision.SelectedExistingFolder?.FullPath));
        PrepareImportDateFolderPreflight(selected);
        foreach (var decision in ImportDateFolderDecisions)
        {
            if (!previous.TryGetValue((decision.Date, decision.SegmentIndex, decision.SegmentCount), out var choice)) continue;
            if (choice.Existing is not null &&
                decision.ExistingFolders.FirstOrDefault(folder => string.Equals(folder.FullPath, choice.Existing, StringComparison.OrdinalIgnoreCase)) is { } folder)
                decision.SelectedExistingFolder = folder;
            decision.Remark = choice.Remark;
            decision.Strategy = choice.Strategy;
        }
        ImportActionHint = "确认每个日期放到哪个文件夹，然后开始导入。导入前不会复制或移动任何文件。";
        NotifyImportStageChanged();
    }
}
