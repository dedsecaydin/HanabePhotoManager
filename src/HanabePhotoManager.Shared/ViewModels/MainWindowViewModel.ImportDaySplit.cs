using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Platform;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Infrastructure.Sessions;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 导入检查页：同一天有长时间间隔时自动分成几个日期文件夹（HANABE-IMPORT-DAY-SPLIT）。
/// 用户可以把整天放回一个文件夹，或把相邻两段放一起。只改变导入目标，不影响“拍摄场次”。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private bool _autoSplitImportDays = true;
    private int _importDaySplitHours = 2;
    private readonly HashSet<LibraryDate> _importDaysKeptTogether = [];
    private readonly HashSet<string> _importMergedSegmentAnchors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _importCaptureTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _importCaptureTimeAttempted = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _importCaptureTimeCancellation;
    private bool _importDayPlanDirty;

    /// <summary>同一天有间隔时自动分文件夹（导入设置，默认开启）。</summary>
    public bool AutoSplitImportDays
    {
        get => _autoSplitImportDays;
        set
        {
            if (!SetProperty(ref _autoSplitImportDays, value)) return;
            OnImportDaySplitOptionsChanged();
        }
    }

    /// <summary>相隔多少小时算另一段：1 / 2 / 3 / 4，默认 2。</summary>
    public int ImportDaySplitHours
    {
        get => _importDaySplitHours;
        set
        {
            var normalized = ImportDaySplitPlanner.ThresholdHourOptions.Contains(value) ? value : 2;
            if (!SetProperty(ref _importDaySplitHours, normalized)) return;
            OnImportDaySplitOptionsChanged();
        }
    }

    public IReadOnlyList<int> ImportDaySplitHourOptions => ImportDaySplitPlanner.ThresholdHourOptions;

    /// <summary>检查页“个拍摄日”：按日期去重（分段后一个日期可能有多个目标文件夹）。</summary>
    public int ImportShootDayCount => ImportDateFolderDecisions.Select(decision => decision.Date).Distinct().Count();

    /// <summary>本次导入一共会放进几个目标文件夹。</summary>
    public int ImportTargetFolderCount => ImportDateFolderDecisions
        .Select(decision => decision.FinalDirectoryPath)
        .Where(path => path.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private void OnImportDaySplitOptionsChanged()
    {
        _ = SaveSettingsAsync();
        _importDaysKeptTogether.Clear();
        _importMergedSegmentAnchors.Clear();
        RebuildImportDayPlan();
    }

    /// <summary>“都放进一个文件夹”：这一天不再分段。</summary>
    [RelayCommand]
    private void KeepImportDayTogether(ImportDateFolderDecision? decision)
    {
        if (decision is null || !CanEditImportDayPlan) return;
        _importDaysKeptTogether.Add(decision.Date);
        RebuildImportDayPlan();
        ImportActionHint = $"{decision.DateText} 已放进同一个文件夹。";
    }

    /// <summary>“分成 N 个文件夹”：恢复自动分段，并清掉这一天手动合并过的段。</summary>
    [RelayCommand]
    private void SplitImportDay(ImportDateFolderDecision? decision)
    {
        if (decision is null || !CanEditImportDayPlan) return;
        _importDaysKeptTogether.Remove(decision.Date);
        var dayKeys = ImportItems.Where(item => item.TargetDate == decision.Date).Select(item => item.PrimaryPath);
        _importMergedSegmentAnchors.ExceptWith(dayKeys);
        RebuildImportDayPlan();
        ImportActionHint = $"{decision.DateText} 已按拍摄间隔分开放。";
    }

    /// <summary>“这两段放一起”：把这一段并到上一段。</summary>
    [RelayCommand]
    private void MergeImportSegmentWithPrevious(ImportDateFolderDecision? decision)
    {
        if (decision is null || !decision.CanMergeWithPrevious || !CanEditImportDayPlan) return;
        _importMergedSegmentAnchors.Add(decision.SegmentAnchorKey);
        RebuildImportDayPlan();
        ImportActionHint = $"{decision.DateText} 的{decision.SegmentLabel}已和上一段放一起；点“分开放”可以恢复。";
    }

    private bool CanEditImportDayPlan => !IsBusy && IsImportDateFolderPreflightVisible;

    private void RebuildImportDayPlan()
    {
        _importDayPlanDirty = true;
        if (IsImportDateFolderPreflightVisible)
        {
            if (IsImportReviewStage) RefreshInlineDatePlan();
            else PrepareImportDateFolderPreflight(_preparedImportItems.Select(prepared => prepared.Item).ToArray());
        }
        OnPropertyChanged(nameof(ImportShootDayCount));
        OnPropertyChanged(nameof(ImportTargetFolderCount));
    }

    /// <summary>拍摄时间：已读到的 EXIF 优先，否则用文件修改时间（相机写卡时就是拍摄时间）。</summary>
    internal DateTime GetImportCaptureTime(ImportPreviewItemViewModel item) =>
        _importCaptureTimes.TryGetValue(item.PrimaryPath, out var time) ? time : item.Group.Primary.LastWriteTime.LocalDateTime;

    /// <summary>测试和外部来源可以直接提供拍摄时间（例如已读过 EXIF）。</summary>
    internal void SetImportCaptureTime(string primaryPath, DateTime localTime)
    {
        _importCaptureTimes[primaryPath] = localTime;
        _importCaptureTimeAttempted[primaryPath] = 1;
    }

    internal IReadOnlyList<ImportDayPlan> BuildImportDayPlans(IReadOnlyList<ImportPreviewItemViewModel> items) =>
        ImportDayPlanBuilder.Build(
            items.Where(item => item.TargetDate is not null)
                .Select(item => new ImportDayItem(item.TargetDate!.Value, item.PrimaryPath, GetImportCaptureTime(item), ImportDayPlanBuilder.PairKeyFor(item.PrimaryPath))),
            _autoSplitImportDays,
            TimeSpan.FromHours(_importDaySplitHours),
            _importDaysKeptTogether,
            _importMergedSegmentAnchors);

    /// <summary>导入目标：每个队列项落到哪个文件夹（按段），找不到段时用当天的整天决定。</summary>
    private Func<ImportPreviewItemViewModel, string?> BuildImportTargetResolver()
    {
        var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byDate = new Dictionary<LibraryDate, string>();
        foreach (var decision in ImportDateFolderDecisions)
        {
            if (decision.MemberKeys.Count == 0) byDate[decision.Date] = decision.FinalDirectoryPath;
            foreach (var key in decision.MemberKeys) byKey[key] = decision.FinalDirectoryPath;
        }
        return item => byKey.TryGetValue(item.PrimaryPath, out var directory) ? directory
            : item.TargetDate is { } date && byDate.TryGetValue(date, out var dayDirectory) ? dayDirectory
            : null;
    }

    /// <summary>
    /// 后台补读 EXIF 拍摄时间：先用修改时间给出方案，读完后如果分段结果变了再静默更新一次。
    /// 只读文件头，不修改任何文件。
    /// </summary>
    private void StartImportCaptureTimeEnrichment(IReadOnlyList<ImportPreviewItemViewModel> items)
    {
        if (!_autoSplitImportDays || PlatformServices.Dispatcher is InlineUiDispatcher) return;
        var pending = items.Select(item => item.PrimaryPath).Where(path => !_importCaptureTimeAttempted.ContainsKey(path)).ToArray();
        if (pending.Length == 0) return;
        _importCaptureTimeCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _importCaptureTimeCancellation = cancellation;
        var token = cancellation.Token;
        var before = DayPlanSignature(items);
        _ = Task.Run(() =>
        {
            var reader = new ExifCaptureTimeReader();
            foreach (var path in pending)
            {
                if (token.IsCancellationRequested) return;
                try
                {
                    var member = reader.Read(path);
                    if (member.LocalTime is { } local && member.Issue is null) _importCaptureTimes[path] = local;
                }
                catch (Exception) { /* 读不到就继续用修改时间 */ }
                _importCaptureTimeAttempted[path] = 1;
            }
        }, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested) return;
            PlatformServices.Dispatcher.Post(() =>
            {
                if (token.IsCancellationRequested || IsBusy || !IsImportDateFolderPreflightVisible) return;
                if (DayPlanSignature(items) != before) RebuildImportDayPlan();
            }, background: true);
        }, TaskScheduler.Default);
    }

    private string DayPlanSignature(IReadOnlyList<ImportPreviewItemViewModel> items) =>
        string.Join("|", BuildImportDayPlans(items).Select(plan => $"{plan.Date}:{plan.AutoSegmentCount}:{string.Join(",", plan.Segments.Select(segment => segment.Count))}"));
}
