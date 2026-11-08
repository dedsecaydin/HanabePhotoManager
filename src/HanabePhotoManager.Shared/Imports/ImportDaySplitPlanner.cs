namespace HanabePhotoManager.App.Imports;

/// <summary>参与“同一天分段”的一个导入单位：Key 是队列项标识，PairKey 相同的项（同名 RAW/JPG 等）永远落在同一段。</summary>
public sealed record DaySplitMember(string Key, DateTime Time, string? PairKey = null);

/// <summary>同一天里按拍摄间隔切出的一段。Index 从 0 开始；GapBefore 是与上一段之间停了多久（第一段为 null）。</summary>
public sealed record DaySegment(int Index, DateTime Start, DateTime End, IReadOnlyList<string> Keys, TimeSpan? GapBefore)
{
    public int Count => Keys.Count;

    /// <summary>第一个成员的 Key，用来稳定标识“这一段”（手动合并按它记住）。</summary>
    public string AnchorKey => Keys[0];
}

/// <summary>
/// 同一天分段：相邻两组间隔 ≥ 阈值就切开；少于 minSegmentSize 组的段并到时间更近的相邻段。
/// 纯逻辑，不读文件。时间用相机本地时间（墙上时间），和“上午 / 下午 / 晚上”的说法一致。
/// </summary>
public static class ImportDaySplitPlanner
{
    public const int DefaultMinSegmentSize = 5;
    public static readonly IReadOnlyList<int> ThresholdHourOptions = [1, 2, 3, 4];

    public static IReadOnlyList<DaySegment> Split(
        IEnumerable<DaySplitMember> members,
        TimeSpan threshold,
        int minSegmentSize = DefaultMinSegmentSize)
    {
        if (threshold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(threshold));
        var source = members.ToArray();
        if (source.Length == 0) return [];

        // 配对项取组内最早时间，整体参与排序，保证不会被拆开。
        var units = source
            .GroupBy(member => member.PairKey ?? "\u0001" + member.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Time: group.Min(member => member.Time), Keys: group.Select(member => member.Key).ToArray()))
            .OrderBy(unit => unit.Time)
            .ThenBy(unit => unit.Keys[0], StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var segments = new List<List<(DateTime Time, string[] Keys)>> { new() { units[0] } };
        for (var i = 1; i < units.Length; i++)
        {
            if (units[i].Time - units[i - 1].Time >= threshold) segments.Add([]);
            segments[^1].Add(units[i]);
        }

        // 零星几张（试拍、测光）并到时间更近的那段；反复合并直到每段都够大或只剩一段。
        int Size(List<(DateTime Time, string[] Keys)> segment) => segment.Sum(unit => unit.Keys.Length);
        while (segments.Count > 1)
        {
            var smallIndex = segments.FindIndex(segment => Size(segment) < minSegmentSize);
            if (smallIndex < 0) break;
            var small = segments[smallIndex];
            var gapBefore = smallIndex > 0 ? small[0].Time - segments[smallIndex - 1][^1].Time : TimeSpan.MaxValue;
            var gapAfter = smallIndex < segments.Count - 1 ? segments[smallIndex + 1][0].Time - small[^1].Time : TimeSpan.MaxValue;
            if (gapBefore <= gapAfter)
            {
                segments[smallIndex - 1].AddRange(small);
            }
            else
            {
                segments[smallIndex + 1].InsertRange(0, small);
            }
            segments.RemoveAt(smallIndex);
        }

        var result = new List<DaySegment>(segments.Count);
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            TimeSpan? gap = i == 0 ? null : segment[0].Time - segments[i - 1][^1].Time;
            result.Add(new DaySegment(i, segment[0].Time, segment[^1].Time,
                segment.SelectMany(unit => unit.Keys).ToArray(), gap));
        }
        return result;
    }

    /// <summary>把 anchorKeys 里那些段并到它前一段（“这两段放一起”）。第一段的 anchor 会被忽略。</summary>
    public static IReadOnlyList<DaySegment> MergeIntoPrevious(IReadOnlyList<DaySegment> segments, IReadOnlySet<string> anchorKeys)
    {
        if (segments.Count <= 1 || anchorKeys.Count == 0) return segments;
        var merged = new List<(DateTime Start, DateTime End, List<string> Keys, TimeSpan? Gap)>();
        foreach (var segment in segments)
        {
            if (merged.Count > 0 && anchorKeys.Contains(segment.AnchorKey))
            {
                var last = merged[^1];
                last.Keys.AddRange(segment.Keys);
                merged[^1] = (last.Start, segment.End, last.Keys, last.Gap);
            }
            else
            {
                merged.Add((segment.Start, segment.End, segment.Keys.ToList(), merged.Count == 0 ? null : segment.GapBefore));
            }
        }
        return merged.Select((segment, index) => new DaySegment(index, segment.Start, segment.End, segment.Keys, index == 0 ? null : segment.Gap)).ToArray();
    }

    /// <summary>按开始时间给的默认备注：上午 / 下午 / 晚上。</summary>
    public static string DefaultRemark(DateTime start) => start.Hour < 12 ? "上午" : start.Hour < 18 ? "下午" : "晚上";

    /// <summary>同一天里备注重名时依次加 2、3：晚上、晚上2、晚上3。</summary>
    public static IReadOnlyList<string> UniqueRemarks(IEnumerable<string> remarks)
    {
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var remark in remarks)
        {
            if (!used.TryGetValue(remark, out var count))
            {
                used[remark] = 1;
                result.Add(remark);
                continue;
            }
            string candidate;
            do { count++; candidate = remark + count; } while (used.ContainsKey(candidate));
            used[remark] = count;
            used[candidate] = 1;
            result.Add(candidate);
        }
        return result;
    }

    /// <summary>“4 小时 50 分”这种说法。</summary>
    public static string FormatGap(TimeSpan gap)
    {
        var hours = (int)gap.TotalHours;
        var minutes = gap.Minutes;
        return hours > 0
            ? minutes > 0 ? $"{hours} 小时 {minutes} 分" : $"{hours} 小时"
            : $"{Math.Max(1, (int)Math.Round(gap.TotalMinutes))} 分钟";
    }
}
