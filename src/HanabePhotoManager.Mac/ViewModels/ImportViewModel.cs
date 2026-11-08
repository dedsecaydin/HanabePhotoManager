using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Mac.Services;

namespace HanabePhotoManager.Mac.ViewModels;

/// <summary>检查页的一张日期卡片：缩略图条、分类比例、重复数、放到哪个文件夹。</summary>
public sealed class DateCardViewModel
{
    public const double BarWidth = 336;

    public DateCardViewModel(ImportDateFolderDecision decision, IReadOnlyList<ImportGroupItem> items, int duplicateCount, string duplicateVerb)
    {
        Decision = decision;
        Items = items;
        Strip = items.GroupBy(item => item.CategoryName).Select(group => group.First()).Concat(items).Distinct().Take(5)
            .Select(item => new ThumbViewModel(item.Group.Primary.FullPath, 120)).ToArray();
        var total = Math.Max(1, items.Count);
        Breakdown = items.GroupBy(item => item.CategoryName).OrderByDescending(group => group.Count())
            .Select(group => new CategoryShare(group.Key, group.Count(), Math.Max(6, Math.Round(BarWidth * group.Count() / total)), CategoryShare.BrushFor(group.Key)))
            .ToArray();
        DuplicateText = duplicateCount > 0 ? $"{duplicateCount} 个重复 · 将{duplicateVerb}" : "无重复";
        var date = new DateTime(decision.Date.Year, decision.Date.Month, decision.Date.Day);
        Weekday = date.ToString("dddd", new System.Globalization.CultureInfo("zh-CN"));
        var bytes = items.Sum(item => item.TotalBytes);
        Summary = $"{items.Count:N0} 组 · {ImportViewModel.FormatBytes(bytes)}";
    }

    public ImportDateFolderDecision Decision { get; }
    public IReadOnlyList<ImportGroupItem> Items { get; }
    public IReadOnlyList<ThumbViewModel> Strip { get; }
    public bool HasOverflow => Items.Count > Strip.Count;
    public string OverflowText => $"+{Items.Count - Strip.Count}";
    public IReadOnlyList<CategoryShare> Breakdown { get; }
    public string DuplicateText { get; }
    public string Weekday { get; }
    public string Summary { get; }

    // 同一天分段（HANABE-IMPORT-DAY-SPLIT），直接取自共享的决定。
    public bool IsSegmented => Decision.IsSegmented;
    public bool IsDayKeptTogether => Decision.IsDayKeptTogether;
    public bool CanMergeWithPrevious => Decision.CanMergeWithPrevious;
    public string SegmentTitle => Decision.IsSegmented ? $"{Decision.SegmentLabel} · {Decision.SegmentTimeText}" : string.Empty;
    public string SegmentGapText => Decision.SegmentGapText;
    public string DaySplitHint => Decision.DaySplitHint;
    public Avalonia.Media.IBrush SegmentFill => Avalonia.Media.Brush.Parse(Decision.SegmentBrush);
}

/// <summary>导入三步：Source（选来源）→ Review（检查确认）→ Running → Done。与 Windows 版流程一致。</summary>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly MacImportEngine _engine = new();
    private CancellationTokenSource? _cancellation;
    private IReadOnlyList<ImportGroupItem> _items = [];
    private IReadOnlyList<ImportDateFolderDecision> _decisions = [];
    private HashSet<string> _duplicates = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _sources = [];
    private readonly HashSet<HanabePhotoManager.Core.Imports.LibraryDate> _daysKeptTogether = [];
    private readonly HashSet<string> _mergedAnchors = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private string _stage = "Source";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _hint = "插入存储卡，或者选择相机文件夹。分析只读，不会复制任何文件。";
    [ObservableProperty] private int _successCount;
    [ObservableProperty] private int _skippedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private string _reportDetails = string.Empty;
    [ObservableProperty] private string _duplicateCountText = "—";

    public ImportViewModel(MainViewModel main)
    {
        _main = main;
        RefreshDevices();
    }

    public ObservableCollection<ImportVolume> Devices { get; } = [];
    public ObservableCollection<DateCardViewModel> DateCards { get; } = [];
    public ObservableCollection<ThumbViewModel> AllItems { get; } = [];

    public bool IsSourceStage => Stage is "Source" or "Analyzing";
    public bool IsAnalyzing => Stage == "Analyzing";
    public bool IsReviewStage => Stage == "Review";
    public bool IsRunningStage => Stage == "Running";
    public bool IsDoneStage => Stage == "Done";
    public bool HasNoDevices => Devices.Count == 0;
    public int SelectedCount => _items.Count(item => item.IsSelected);
    public int DateCount => _decisions.Select(decision => decision.Date).Distinct().Count();
    public string TotalSize => FormatBytes(_items.Where(item => item.IsSelected).Sum(item => item.TotalBytes));
    public int UnresolvedCount => _items.Count(item => item.IsSelected && item.NeedsAttention);
    public bool HasUnresolved => UnresolvedCount > 0;
    public string SourcesText => string.Join("  ·  ", _sources.Select(Path.GetFileName));
    public string StepState1 => Stage is "Source" or "Analyzing" ? "active" : "done";
    public string StepState2 => Stage is "Review" ? "active" : Stage is "Running" or "Done" ? "done" : "todo";
    public string StepState3 => Stage is "Running" ? "active" : Stage is "Done" ? "done" : "todo";

    public bool MoveAfterVerify
    {
        get => _main.Settings.MoveAfterVerify;
        set { _main.Settings.MoveAfterVerify = value; _main.SaveSettings(settings => settings.MacMoveAfterVerify = value); OnPropertyChanged(); }
    }

    public bool SkipDuplicates
    {
        get => _main.Settings.SkipDuplicates;
        set { _main.Settings.SkipDuplicates = value; _main.SaveSettings(settings => settings.MacSkipDuplicates = value); OnPropertyChanged(); RebuildCards(); }
    }

    /// <summary>同一天有间隔时自动分文件夹（与 Windows 共用同一个设置）。</summary>
    public bool AutoSplitImportDays
    {
        get => _main.Settings.AutoSplitImportDays;
        set
        {
            _main.Settings.AutoSplitImportDays = value;
            _main.SaveSettings(settings => settings.AutoSplitImportDays = value);
            OnPropertyChanged();
            RebuildDecisions();
        }
    }

    public IReadOnlyList<int> DaySplitHourOptions => ImportDaySplitPlanner.ThresholdHourOptions;

    public int ImportDaySplitHours
    {
        get => _main.Settings.ImportDaySplitHours;
        set
        {
            var hours = ImportDaySplitPlanner.ThresholdHourOptions.Contains(value) ? value : 2;
            _main.Settings.ImportDaySplitHours = hours;
            _main.SaveSettings(settings => settings.ImportDaySplitHours = hours);
            OnPropertyChanged();
            RebuildDecisions();
        }
    }

    [RelayCommand]
    private void KeepDayTogether(DateCardViewModel? card)
    {
        if (card is null || Stage != "Review") return;
        _daysKeptTogether.Add(card.Decision.Date);
        RebuildDecisions();
    }

    [RelayCommand]
    private void SplitDay(DateCardViewModel? card)
    {
        if (card is null || Stage != "Review") return;
        _daysKeptTogether.Remove(card.Decision.Date);
        _mergedAnchors.ExceptWith(_items.Where(item => item.Date == card.Decision.Date).Select(item => item.Group.Primary.FullPath));
        RebuildDecisions();
    }

    [RelayCommand]
    private void MergeWithPrevious(DateCardViewModel? card)
    {
        if (card is null || Stage != "Review" || !card.CanMergeWithPrevious) return;
        _mergedAnchors.Add(card.Decision.SegmentAnchorKey);
        RebuildDecisions();
    }

    /// <summary>重新生成日期方案（分段方式变化时），按（日期, 第几段, 共几段）保留已填的备注与处理方式。</summary>
    private void RebuildDecisions()
    {
        if (_items.Count == 0 || string.IsNullOrWhiteSpace(_main.Settings.LibraryRoot)) return;
        var previous = _decisions.ToDictionary(d => (d.Date, d.SegmentIndex, d.SegmentCount), d => (d.Strategy, d.Remark));
        _decisions = CreateDecisions();
        foreach (var decision in _decisions)
        {
            if (!previous.TryGetValue((decision.Date, decision.SegmentIndex, decision.SegmentCount), out var choice)) continue;
            decision.Remark = choice.Remark;
            decision.Strategy = choice.Strategy;
        }
        RebuildCards();
    }

    private IReadOnlyList<ImportDateFolderDecision> CreateDecisions() => _engine.CreateDecisions(
        _main.Settings.LibraryRoot, _items, AutoSplitImportDays, TimeSpan.FromHours(ImportDaySplitHours), _daysKeptTogether, _mergedAnchors);

    partial void OnStageChanged(string value)
    {
        foreach (var name in new[] { nameof(IsSourceStage), nameof(IsAnalyzing), nameof(IsReviewStage), nameof(IsRunningStage), nameof(IsDoneStage), nameof(StepState1), nameof(StepState2), nameof(StepState3) })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var volume in VolumeService.List(_main.Settings.LibraryRoot)) Devices.Add(volume);
        OnPropertyChanged(nameof(HasNoDevices));
    }

    [RelayCommand]
    private Task ImportDevice(string? path) => string.IsNullOrWhiteSpace(path) ? Task.CompletedTask : AnalyzeAsync([path]);

    /// <summary>选择文件夹（由窗口提供系统文件夹选择框）后分析。</summary>
    [RelayCommand]
    private async Task ChooseFolders()
    {
        var folders = await _main.PickFoldersAsync("选择相机来源文件夹", allowMultiple: true);
        if (folders.Count > 0) await AnalyzeAsync(folders);
    }

    public async Task AnalyzeAsync(IReadOnlyList<string> roots)
    {
        if (Stage is "Analyzing" or "Running") return;
        if (string.IsNullOrWhiteSpace(_main.Settings.LibraryRoot))
        {
            Hint = "先在「设置」里选择照片库位置。";
            return;
        }

        _cancellation = new CancellationTokenSource();
        _sources = roots;
        Stage = "Analyzing";
        ProgressText = "正在读取来源…";
        try
        {
            var progress = new Progress<string>(text => ProgressText = text);
            _items = await _engine.AnalyzeAsync(roots, progress, _cancellation.Token);
            if (_items.Count == 0)
            {
                Stage = "Source";
                Hint = "没有找到可导入的照片或视频，换一个文件夹试试。";
                return;
            }

            ProgressText = "正在和照片库里同一天的文件夹比对重复…";
            _daysKeptTogether.Clear();
            _mergedAnchors.Clear();
            _decisions = CreateDecisions();
            _duplicates = await _engine.DetectDuplicatesAsync(_main.Settings.LibraryRoot, _items, _cancellation.Token);
            RebuildCards();
            AllItems.Clear();
            foreach (var item in _items.Take(400)) AllItems.Add(new ThumbViewModel(item.Group.Primary.FullPath, 160));
            Stage = "Review";
            Hint = HasUnresolved
                ? $"{UnresolvedCount} 组读不到日期或无法判断分类，这一版会先跳过它们。"
                : "看一下每个日期放到哪个文件夹，然后开始导入。导入前不会复制或移动任何文件。";
        }
        catch (OperationCanceledException)
        {
            Stage = "Source";
            Hint = "已停止分析。";
        }
        catch (Exception ex)
        {
            Stage = "Source";
            Hint = "分析失败：" + ex.Message;
        }
    }

    private void RebuildCards()
    {
        DateCards.Clear();
        var verb = SkipDuplicates ? "跳过" : "仍然导入";
        foreach (var decision in _decisions)
        {
            var items = _items.Where(item => item.IsSelected && MacImportEngine.Covers(decision, item)).ToArray();
            DateCards.Add(new DateCardViewModel(decision, items, items.Count(item => _duplicates.Contains(item.Group.Primary.FullPath)), verb));
        }
        DuplicateCountText = _duplicates.Count.ToString("N0");
        foreach (var name in new[] { nameof(SelectedCount), nameof(DateCount), nameof(TotalSize), nameof(UnresolvedCount), nameof(HasUnresolved), nameof(SourcesText) })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private async Task StartImport()
    {
        if (Stage != "Review") return;
        if (MoveAfterVerify && !await _main.ConfirmAsync("校验后移动", "每个文件复制并逐个校验 SHA-256 一致后，才会从存储卡上删除原文件。确定使用「校验后移动」吗？"))
            return;

        _cancellation = new CancellationTokenSource();
        Stage = "Running";
        ProgressValue = 0;
        try
        {
            var progress = new Progress<(int Done, int Total, string Name)>(update =>
            {
                ProgressValue = update.Total == 0 ? 100 : update.Done * 100d / update.Total;
                ProgressText = update.Name.Length == 0 ? "正在收尾…" : $"正在导入 {update.Done + 1:N0} / {update.Total:N0} · {update.Name}";
            });
            var skip = SkipDuplicates ? _duplicates : new HashSet<string>();
            var result = await _engine.RunAsync(_main.Settings.LibraryRoot, _decisions, _items, MoveAfterVerify, skip, progress, _cancellation.Token);
            SuccessCount = result.Success;
            SkippedCount = result.Skipped;
            FailedCount = result.Failed;
            ReportDetails = string.Join(Environment.NewLine, result.Lines);
            Stage = "Done";
            Hint = result.Lines.FirstOrDefault() ?? string.Empty;
            _main.Library.Refresh();
        }
        catch (OperationCanceledException)
        {
            Stage = "Review";
            Hint = "已安全停止：已完成的文件保留在照片库，没完成的临时文件已清理。";
        }
        catch (Exception ex)
        {
            Stage = "Review";
            Hint = "导入没有完成：" + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    public void CancelRunning() => _cancellation?.Cancel();

    [RelayCommand]
    private void StartOver()
    {
        if (Stage is "Analyzing" or "Running") return;
        _items = [];
        _decisions = [];
        _duplicates.Clear();
        _sources = [];
        DateCards.Clear();
        AllItems.Clear();
        SuccessCount = SkippedCount = FailedCount = 0;
        ReportDetails = string.Empty;
        Stage = "Source";
        Hint = "插入存储卡，或者选择相机文件夹。分析只读，不会复制任何文件。";
        RefreshDevices();
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):0.##} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.##} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MB",
        _ => $"{Math.Max(0, bytes) / 1024d:0} KB",
    };
}
