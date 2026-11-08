using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

/// <summary>
/// 一个日期的导入分段方案。Segments 为空或只有 1 段表示整天一个文件夹；
/// AutoSegmentCount 是自动规则能分出的段数（用户选“都放进一个文件夹”后仍记录，界面据此显示切换）。
/// </summary>
public sealed record ImportDayPlan(
    LibraryDate Date,
    int MediaCount,
    IReadOnlyList<DaySegment> Segments,
    int AutoSegmentCount,
    bool KeptTogether);

/// <summary>参与分段的一个导入项：日期、稳定标识（主文件路径）、拍摄时间、配对标识（同目录同名）。</summary>
public sealed record ImportDayItem(LibraryDate Date, string Key, DateTime Time, string PairKey);

/// <summary>Windows 与 macOS 共用：把导入项按日期生成分段方案（HANABE-IMPORT-DAY-SPLIT）。</summary>
public static class ImportDayPlanBuilder
{
    public static IReadOnlyList<ImportDayPlan> Build(
        IEnumerable<ImportDayItem> items,
        bool autoSplit,
        TimeSpan threshold,
        IReadOnlySet<LibraryDate>? keptTogether = null,
        IReadOnlySet<string>? mergedAnchors = null)
    {
        keptTogether ??= new HashSet<LibraryDate>();
        mergedAnchors ??= new HashSet<string>();
        return items
            .GroupBy(item => item.Date)
            .Select(day =>
            {
                var dayItems = day.ToArray();
                if (!autoSplit)
                    return new ImportDayPlan(day.Key, dayItems.Length, [], 1, false);
                var auto = ImportDaySplitPlanner.Split(
                    dayItems.Select(item => new DaySplitMember(item.Key, item.Time, item.PairKey)), threshold);
                if (auto.Count <= 1)
                    return new ImportDayPlan(day.Key, dayItems.Length, [], 1, false);
                if (keptTogether.Contains(day.Key))
                    return new ImportDayPlan(day.Key, dayItems.Length, [], auto.Count, true);
                var segments = ImportDaySplitPlanner.MergeIntoPrevious(auto, mergedAnchors);
                return new ImportDayPlan(day.Key, dayItems.Length, segments.Count > 1 ? segments : [], auto.Count, segments.Count <= 1);
            })
            .ToArray();
    }

    /// <summary>同目录同名（不含扩展名）视为一对：RAW + JPG、视频 + 封面。</summary>
    public static string PairKeyFor(string primaryPath) =>
        Path.Combine(Path.GetDirectoryName(primaryPath) ?? string.Empty, Path.GetFileNameWithoutExtension(primaryPath));
}
