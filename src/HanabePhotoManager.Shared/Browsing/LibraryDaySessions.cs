using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Browsing;

/// <summary>已导入日期文件夹里的一「场」：同一天里按拍摄间隔切开的一段。Paths 是这一场的全部媒体文件。</summary>
public sealed record LibraryDaySession(
    int Index,
    string Remark,
    DateTime Start,
    DateTime End,
    TimeSpan? GapBefore,
    IReadOnlyList<string> Paths,
    bool ShowDate = false)
{
    public int Count => Paths.Count;

    /// <summary>「09:12–11:40」；场次跨了不同日期时带日期「11.08 08:35–08:39」。</summary>
    public string TimeText => Start.Date != End.Date
        ? $"{Start:MM.dd HH:mm}–{End:MM.dd HH:mm}"
        : ShowDate ? $"{Start:MM.dd} {Start:HH:mm}–{End:HH:mm}" : $"{Start:HH:mm}–{End:HH:mm}";

    /// <summary>「距上一场 4 小时 50 分」；第一场为空。</summary>
    public string GapText => GapBefore is { } gap ? $"距上一场 {ImportDaySplitPlanner.FormatGap(gap)}" : string.Empty;
}

/// <summary>一次「按场次拆分到文件夹」的执行计划：目标文件夹（第 1 场留在原文件夹）+ 需要移动的文件。</summary>
public sealed record LibraryDaySplitPlan(
    string DateFolder,
    IReadOnlyList<string> TargetFolders,
    IReadOnlyList<LibraryFileMove> Moves,
    IReadOnlyList<int> FilesPerFolder);

public sealed record LibraryFileMove(string Source, string Target);

/// <summary>
/// 已导入日期的「虚拟分场」与「一键拆分」：和导入时的同日分段用同一套规则（ImportDaySplitPlanner：
/// 间隔 ≥ 阈值切开，小于 5 张的零碎段并到相邻场；同名 RAW/JPG/视频封面/XML 永远在同一场）。
/// 时间用文件的修改时间（相机写卡时间，导入复制时保留）；「修后」不参与计时，按同名原片归场，拆分时也不移动。
/// </summary>
public static class LibraryDaySessionPlanner
{
    public const string RetouchedFolderName = "修后";

    private static readonly HashSet<string> InnerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".cr2", ".cr3", ".nef", ".raf", ".rw2", ".orf", ".dng", ".raw",
        ".jpg", ".jpeg", ".heic", ".heif", ".hif", ".png", ".tif", ".tiff", ".psd",
        ".mp4", ".mov", ".mts", ".m2ts", ".avi", ".mkv",
    };

    private static readonly Regex SonyXmlSuffix = new(@"(?<=^|[(（])(C\d+)M\d{2}(?=[)）]?$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>同一张照片 / 同一段视频的各个文件（RAW、JPG、封面 T01、XML M01、xmp 等）共用的键：日期文件夹无关，只看文件名。</summary>
    public static string StemKey(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        // x.ARW.xmp / x.JPG.acr 这种「双扩展名」边车：再去掉一层媒体扩展名
        if (InnerExtensions.Contains(Path.GetExtension(stem))) stem = Path.GetFileNameWithoutExtension(stem);
        stem = ThumbnailResolver.VideoStemForSonyPoster(stem);
        stem = SonyXmlSuffix.Replace(stem, "$1");
        return stem.ToUpperInvariant();
    }

    public static bool IsRetouched(string category) =>
        string.Equals(category, RetouchedFolderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>把一天（一个日期文件夹）的文件按拍摄间隔分场；只有一场时返回单个元素。</summary>
    public static IReadOnlyList<LibraryDaySession> Plan(
        IEnumerable<(string Path, string Category, DateTime Time)> items,
        TimeSpan threshold,
        int minSegmentSize = ImportDaySplitPlanner.DefaultMinSegmentSize) =>
        Plan(items.Select(item => (item.Path, item.Category, (DateTime?)item.Time)), threshold, minSegmentSize);

    /// <summary>
    /// 同上；Time 为 null 的文件（读不到拍摄时间）不参与计时，按同名文件归场，找不到就归第 1 场（拆分时留在原文件夹）。
    /// 全部都没有拍摄时间时不分场。
    /// </summary>
    public static IReadOnlyList<LibraryDaySession> Plan(
        IEnumerable<(string Path, string Category, DateTime? Time)> items,
        TimeSpan threshold,
        int minSegmentSize = ImportDaySplitPlanner.DefaultMinSegmentSize)
    {
        var all = items.ToArray();
        if (all.Length == 0) return [];
        var timed = all.Where(item => item.Time is not null && !IsRetouched(item.Category)).ToArray();
        if (timed.Length == 0)
            return [new LibraryDaySession(0, string.Empty, default, default, null, all.Select(item => item.Path).ToArray())];

        var segments = ImportDaySplitPlanner.Split(
            timed.Select(item => new DaySplitMember(item.Path, item.Time!.Value, StemKey(item.Path))),
            threshold,
            minSegmentSize);
        var multiDay = segments.Select(segment => segment.Start.Date).Distinct().Count() > 1;
        var remarks = ImportDaySplitPlanner.UniqueRemarks(segments.Select(segment =>
            (multiDay && segment.Start.Date != segments[0].Start.Date ? segment.Start.ToString("MM.dd", CultureInfo.InvariantCulture) : string.Empty)
            + ImportDaySplitPlanner.DefaultRemark(segment.Start)));

        var paths = segments.Select(segment => segment.Keys.ToList()).ToArray();
        var stemToSession = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < segments.Count; i++)
            foreach (var key in segments[i].Keys)
                stemToSession.TryAdd(StemKey(key), i);
        var timedSet = new HashSet<string>(timed.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var item in all.Where(item => !timedSet.Contains(item.Path)))
            paths[stemToSession.GetValueOrDefault(StemKey(item.Path), 0)].Add(item.Path);

        return segments
            .Select((segment, i) => new LibraryDaySession(i, remarks[i], segment.Start, segment.End, segment.GapBefore, paths[i], multiDay))
            .ToArray();
    }

    /// <summary>
    /// 每一场的目标文件夹：第 1 场留在原文件夹；其余在同一月份目录下新建「11.07_晚上」，重名时加 2、3。
    /// </summary>
    public static IReadOnlyList<string> TargetFolders(string dateFolder, IReadOnlyList<LibraryDaySession> sessions, Func<string, bool> exists)
    {
        var folder = dateFolder.TrimEnd('\\', '/');
        var parent = Path.GetDirectoryName(folder) ?? string.Empty;
        var name = Path.GetFileName(folder);
        var datePart = name.Split('_', 2)[0];
        var result = new List<string> { folder };
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { folder };
        foreach (var session in sessions.Skip(1))
        {
            var remark = LibraryDateFolderService.NormalizeRemarkForFolderName(session.Remark);
            var candidate = Path.Combine(parent, $"{datePart}_{remark}");
            for (var n = 2; exists(candidate) || taken.Contains(candidate); n++)
                candidate = Path.Combine(parent, $"{datePart}_{remark}{n}");
            taken.Add(candidate);
            result.Add(candidate);
        }
        return result;
    }

    /// <summary>
    /// 生成移动清单：日期文件夹下（含 RAW直出 / JPG直出 / 视频 等子目录）的每个文件按 StemKey 找到所属场次，
    /// 第 2 场起移到对应新文件夹的同名子目录；「修后」整个不动；找不到场次的（未知边车等）留在原地。
    /// </summary>
    public static LibraryDaySplitPlan BuildSplitPlan(
        string dateFolder,
        IReadOnlyList<LibraryDaySession> sessions,
        IEnumerable<string> filesInFolder,
        Func<string, bool> exists)
    {
        var folder = dateFolder.TrimEnd('\\', '/');
        var targets = TargetFolders(folder, sessions, exists);
        var stemToSession = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
            foreach (var path in session.Paths)
                stemToSession.TryAdd(StemKey(path), session.Index);

        var moves = new List<LibraryFileMove>();
        var counts = new int[targets.Count];
        foreach (var file in filesInFolder)
        {
            var relative = Path.GetRelativePath(folder, file);
            var firstSegment = relative.Split('\\', '/')[0];
            if (string.Equals(firstSegment, RetouchedFolderName, StringComparison.OrdinalIgnoreCase)) continue;
            if (relative.StartsWith("..", StringComparison.Ordinal)) continue;
            var index = stemToSession.GetValueOrDefault(StemKey(file), 0);
            counts[index]++;
            if (index == 0) continue;
            moves.Add(new LibraryFileMove(file, Path.Combine(targets[index], relative)));
        }
        return new LibraryDaySplitPlan(folder, targets, moves, counts);
    }
}
