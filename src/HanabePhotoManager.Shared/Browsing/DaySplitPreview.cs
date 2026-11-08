using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HanabePhotoManager.App.Browsing;

/// <summary>一次重新分场的结果（拖动间隔滑块时生成）。</summary>
public sealed record DaySplitPreviewPlan(IReadOnlyList<DaySplitPreviewSession> Sessions, int TotalMoves);

/// <summary>
/// 「按场次拆分到文件夹」确认窗口的数据：分场间隔可以用滑块调（实时重新分场），
/// 每一场的时间、数量、去向；新文件夹的备注可以在窗口里改。
/// </summary>
public sealed partial class DaySplitPreview : ObservableObject
{
    public const double MinimumHours = 0.5;
    public const double MaximumHours = 8;

    private readonly Func<double, DaySplitPreviewPlan> _replan;

    public DaySplitPreview(string folderName, double thresholdHours, Func<double, DaySplitPreviewPlan> replan)
    {
        FolderName = folderName;
        _replan = replan;
        _thresholdHours = Math.Clamp(thresholdHours, MinimumHours, MaximumHours);
        Apply(_replan(_thresholdHours));
    }

    public string FolderName { get; }
    public ObservableCollection<DaySplitPreviewSession> Sessions { get; } = [];
    public int TotalMoves { get; private set; }

    /// <summary>两张照片间隔超过这么久就算新的一场（小时，步长 0.5）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThresholdText))]
    private double _thresholdHours;

    public string ThresholdText => FormatHours(ThresholdHours);
    public string Subtitle => $"「{FolderName}」· 已导入的照片按拍摄时间重新分场";
    public string SummaryText => Sessions.Count > 1
        ? $"分成 {Sessions.Count} 场，移动 {TotalMoves:N0} 个文件"
        : "这个间隔下只有 1 场，不需要拆分";
    public bool CanSplit => Sessions.Count > 1 && TotalMoves > 0;
    public string ConfirmText => CanSplit ? $"拆成 {Sessions.Count} 个文件夹" : "无需拆分";

    partial void OnThresholdHoursChanged(double value) => Apply(_replan(value));

    private void Apply(DaySplitPreviewPlan plan)
    {
        // 改过的备注：同一场（时间相同）重新分场后保留
        var edited = Sessions
            .Where(session => session.IsNew && session.Remark.Trim() != session.DefaultRemark)
            .ToDictionary(session => session.TimeText, session => session.Remark);
        Sessions.Clear();
        foreach (var session in plan.Sessions)
        {
            if (session.IsNew && edited.TryGetValue(session.TimeText, out var remark)) session.Remark = remark;
            Sessions.Add(session);
        }
        TotalMoves = plan.TotalMoves;
        OnPropertyChanged(nameof(TotalMoves));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(CanSplit));
        OnPropertyChanged(nameof(ConfirmText));
    }

    public static string FormatHours(double hours) =>
        hours < 1 ? $"{hours * 60:0} 分钟" : hours % 1 == 0 ? $"{hours:0} 小时" : $"{hours:0.#} 小时";

    /// <summary>备注不能为空、不能重复、不能含文件名非法字符；返回错误说明，没问题返回 null。</summary>
    public string? Validate()
    {
        if (!CanSplit) return "这个间隔下只有 1 场，不需要拆分。把间隔调小一点试试。";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in Sessions.Where(session => session.IsNew))
        {
            var remark = session.Remark.Trim();
            if (remark.Length == 0) return $"{session.Label}的文件夹备注不能为空。";
            if (remark.IndexOfAny(invalid) >= 0) return $"{session.Label}的备注里有不能用在文件夹名里的字符。";
            if (!seen.Add(remark)) return $"备注「{remark}」重复了，换一个吧。";
        }
        return null;
    }
}

public sealed partial class DaySplitPreviewSession : ObservableObject
{
    private static readonly string[] Palette = ["#FF6FA5", "#B9A2FF", "#7EE0C3", "#FFB36B"];

    public DaySplitPreviewSession(int index, string remark, string timeText, int count, string gapText, string datePart, string? keepFolderName)
    {
        Index = index;
        DefaultRemark = remark;
        _remark = remark;
        TimeText = timeText;
        Count = count;
        GapText = gapText;
        DatePart = datePart;
        KeepFolderName = keepFolderName;
    }

    public int Index { get; }
    public string Label => $"第 {Index + 1} 场";
    public string DefaultRemark { get; }
    public string TimeText { get; }
    public int Count { get; }
    public string CountText => $"{Count:N0} 个文件";
    public string GapText { get; }
    public bool HasGap => GapText.Length > 0;
    public string DatePart { get; }
    /// <summary>第 1 场留在原文件夹：原文件夹名；其余为 null（新建）。</summary>
    public string? KeepFolderName { get; }
    public bool IsNew => KeepFolderName is null;
    public string AccentColor => Palette[Index % Palette.Length];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetFolderName))]
    private string _remark;

    public string TargetFolderName => KeepFolderName ?? $"{DatePart}_{Remark.Trim()}";
}
