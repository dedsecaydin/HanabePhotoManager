using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

public enum ImportDateFolderStrategy
{
    Unselected,
    CreateSeparate,
    RenameExisting,
    UseExisting,
}

public sealed record ImportDateFolderCandidate(string Name, string FullPath, string Remark);

public sealed record ImportDateFolderStrategyOption(ImportDateFolderStrategy Value, string DisplayName);

public sealed class ImportDateFolderDecision : ObservableObject
{
    private readonly Action<ImportDateFolderDecision> _refresh;
    private string _remark = string.Empty;
    private ImportDateFolderStrategy _strategy;
    private ImportDateFolderCandidate? _selectedExistingFolder;

    internal ImportDateFolderDecision(
        LibraryDate date,
        int mediaCount,
        IReadOnlyList<ImportDateFolderCandidate> existingFolders,
        Action<ImportDateFolderDecision> refresh)
    {
        Date = date;
        MediaCount = mediaCount;
        ExistingFolders = existingFolders;
        _refresh = refresh;
        _strategy = existingFolders.Count == 0
            ? ImportDateFolderStrategy.CreateSeparate
            : ImportDateFolderStrategy.UseExisting;
        _selectedExistingFolder = existingFolders.FirstOrDefault();
    }

    public static IReadOnlyList<ImportDateFolderStrategyOption> StrategyOptions { get; } =
    [
        new(ImportDateFolderStrategy.CreateSeparate, "1. 同日期新建不同备注文件夹"),
        new(ImportDateFolderStrategy.RenameExisting, "2. 修改原文件夹备注并导入"),
        new(ImportDateFolderStrategy.UseExisting, "3. 使用原文件夹名，不更改"),
    ];

    public IReadOnlyList<ImportDateFolderStrategyOption> AvailableStrategies => StrategyOptions;

    public LibraryDate Date { get; }
    public int MediaCount { get; }
    public string DateText => $"{Date.Month:00}.{Date.Day:00}";
    public IReadOnlyList<ImportDateFolderCandidate> ExistingFolders { get; }
    public bool HasExistingFolders => ExistingFolders.Count > 0;

    public string Remark
    {
        get => _remark;
        set
        {
            if (SetProperty(ref _remark, value ?? string.Empty)) _refresh(this);
        }
    }

    public ImportDateFolderStrategy Strategy
    {
        get => _strategy;
        set
        {
            if (SetProperty(ref _strategy, value))
            {
                if (value == ImportDateFolderStrategy.RenameExisting &&
                    _selectedExistingFolder is not null && string.IsNullOrWhiteSpace(_remark))
                {
                    _remark = _selectedExistingFolder.Remark;
                    OnPropertyChanged(nameof(Remark));
                }
                _refresh(this);
            }
        }
    }

    public ImportDateFolderCandidate? SelectedExistingFolder
    {
        get => _selectedExistingFolder;
        set
        {
            if (SetProperty(ref _selectedExistingFolder, value))
            {
                if (_strategy == ImportDateFolderStrategy.RenameExisting &&
                    value is not null && string.IsNullOrWhiteSpace(_remark))
                {
                    _remark = value.Remark;
                    OnPropertyChanged(nameof(Remark));
                }
                _refresh(this);
            }
        }
    }

    private string _duplicateSummary = string.Empty;

    /// <summary>第 2 步后台查重的结果，例如“3 个重复 · 将跳过”；空字符串表示未检查或无重复。</summary>
    public string DuplicateSummary
    {
        get => _duplicateSummary;
        internal set => SetProperty(ref _duplicateSummary, value ?? string.Empty);
    }

    // ---------- 同一天分段（HANABE-IMPORT-DAY-SPLIT） ----------

    /// <summary>这一天被分成几段；1 表示整天一个文件夹。</summary>
    public int SegmentCount { get; private set; } = 1;
    /// <summary>当天第几段，从 0 开始。</summary>
    public int SegmentIndex { get; private set; }
    public DateTime? SegmentStart { get; private set; }
    public DateTime? SegmentEnd { get; private set; }
    /// <summary>与上一段之间停了多久；第一段为空。</summary>
    public TimeSpan? GapBefore { get; private set; }
    /// <summary>这一段包含的队列项（PrimaryPath）。整天一个文件夹时为空，表示“当天全部”。</summary>
    public IReadOnlyList<string> MemberKeys { get; private set; } = [];
    /// <summary>当天能分出的段数（自动规则下）；大于 1 时界面提供“分开 / 都放一个文件夹”的切换。</summary>
    public int AutoSegmentCount { get; private set; } = 1;
    /// <summary>用户已选择“都放进一个文件夹”。</summary>
    public bool IsDayKeptTogether { get; private set; }

    public bool IsSegmented => SegmentCount > 1;
    public bool CanSplitDay => AutoSegmentCount > 1;
    public bool CanMergeWithPrevious => IsSegmented && SegmentIndex > 0;
    public string SegmentAnchorKey => MemberKeys.Count > 0 ? MemberKeys[0] : string.Empty;
    public string SegmentLabel => IsSegmented ? $"第 {SegmentIndex + 1} 段" : string.Empty;
    public string SegmentTimeText => SegmentStart is { } start && SegmentEnd is { } end ? $"{start:HH:mm} – {end:HH:mm}" : string.Empty;
    public string SegmentGapText => GapBefore is { } gap ? $"与上一段相隔 {ImportDaySplitPlanner.FormatGap(gap)}" : string.Empty;
    /// <summary>整天卡片上的提示，例如“这一天拍了 2 段 · 已合成一个文件夹”。</summary>
    public string DaySplitHint => IsSegmented
        ? $"这一天拍了 {SegmentCount} 段，分开放"
        : IsDayKeptTogether && AutoSegmentCount > 1 ? $"这一天拍了 {AutoSegmentCount} 段，已放进同一个文件夹" : string.Empty;
    public bool HasDaySplitHint => DaySplitHint.Length > 0;
    /// <summary>分段色点：与设计稿一致，第 1 段强调粉，其余依次轮换。</summary>
    public string SegmentBrush => SegmentPalette[SegmentIndex % SegmentPalette.Length];

    private static readonly string[] SegmentPalette = ["#FF6FA5", "#B9A2FF", "#7EE0C3", "#FFB36B"];

    internal void ApplySegment(DaySegment? segment, int segmentCount, int autoSegmentCount, bool keptTogether, IReadOnlyList<string> memberKeys)
    {
        SegmentCount = Math.Max(1, segmentCount);
        SegmentIndex = segment?.Index ?? 0;
        SegmentStart = segment?.Start;
        SegmentEnd = segment?.End;
        GapBefore = segment?.GapBefore;
        MemberKeys = memberKeys;
        AutoSegmentCount = Math.Max(1, autoSegmentCount);
        IsDayKeptTogether = keptTogether;
    }

    /// <summary>预填一个默认备注（不触发校验刷新；由预检服务随后统一刷新）。</summary>
    internal void PresetStrategy(ImportDateFolderStrategy strategy, string remark)
    {
        _strategy = strategy;
        _remark = remark;
    }

    public string FinalDirectoryName { get; internal set; } = string.Empty;
    public string FinalDirectoryPath { get; internal set; } = string.Empty;
    public string ValidationMessage { get; internal set; } = string.Empty;
    public bool IsValid { get; internal set; }

    internal void PublishResolution()
    {
        OnPropertyChanged(nameof(FinalDirectoryName));
        OnPropertyChanged(nameof(FinalDirectoryPath));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(IsValid));
    }
}
