namespace HanabePhotoManager.Core.Sessions;
public sealed record CaptureMember(string Id, string Path, DateTime? LocalTime, TimeSpan? UtcOffset, string Camera, string? Issue = null, DateTime? EffectiveTime = null)
{
    public string Name => System.IO.Path.GetFileName(Path);
}
public sealed record SessionGroup(string Id, string Name, IReadOnlyList<CaptureMember> Members, bool NeedsReview = false)
{
    public DateTime? Start => Members.Where(m => m.LocalTime.HasValue).Select(m => m.EffectiveTime ?? m.LocalTime).Min();
    public DateTime? End => Members.Where(m => m.LocalTime.HasValue).Select(m => m.EffectiveTime ?? m.LocalTime).Max();
    public int Count => Members.Count;
    public string TimeBasis => Members.All(m => m.UtcOffset.HasValue) ? "UTC，含相机修正" : Members.All(m => !m.UtcOffset.HasValue) ? "相机本地时间，未推测时区" : "混合时钟，需确认";
    public string Range => Start.HasValue ? $"{Start:yyyy-MM-dd HH:mm:ss} — {End:yyyy-MM-dd HH:mm:ss}（{TimeBasis}）" : "拍摄时间缺失";
}
public sealed record SessionPreview(IReadOnlyList<SessionGroup> Groups);
public sealed record SessionPreferences(double ThresholdMinutes = 30, string Sort = "时间", Dictionary<string, double>? CameraCorrections = null);
public sealed record SessionDocument(int Version, long Revision, IReadOnlyList<SessionGroup> Groups, IReadOnlyList<SessionGroup>? Previous, SessionPreferences Preferences, SessionPreferences? PreviousPreferences = null)
{
    public static SessionDocument Empty => new(1, 0, [], null, new());
}
public interface ISessionStore
{
    Task<SessionDocument> LoadAsync(CancellationToken cancellationToken = default);
    Task<SessionDocument> CommitAsync(long expectedRevision, IReadOnlyList<SessionGroup> groups, SessionPreferences preferences, CancellationToken cancellationToken = default);
    Task<SessionDocument> UndoAsync(long expectedRevision, CancellationToken cancellationToken = default);
}
public interface ICaptureTimeReader { CaptureMember Read(string path); }
public static class SessionPlanner
{
    public static SessionPreview Detect(IEnumerable<CaptureMember> source, TimeSpan? threshold = null, IReadOnlyDictionary<string, double>? corrections = null)
    {
        var gap = threshold ?? TimeSpan.FromMinutes(30);
        if (gap <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(threshold));
        DateTime Effective(CaptureMember member)
        {
            try
            {
                return member.LocalTime!.Value
                    .AddMinutes(corrections?.GetValueOrDefault(member.Camera) ?? 0)
                    .Subtract(member.UtcOffset ?? TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new ArgumentException($"素材“{member.Name}”的拍摄时间与相机修正超出有效日期范围。请检查原始时间、时区和修正分钟数。", nameof(corrections), exception);
            }
        }
        if (corrections?.Values.Any(v => !double.IsFinite(v) || Math.Abs(v) > 525600) == true) throw new ArgumentOutOfRangeException(nameof(corrections));
        var members = source.ToArray();
        if (members.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Length) throw new ArgumentException("重复成员标识。", nameof(source));
        var hasReliableOffset = members.Any(member => member.UtcOffset.HasValue);
        var hasDifferentCameras = members.Select(member => member.Camera).Distinct(StringComparer.Ordinal).Take(2).Count() > 1;
        var groups = new List<SessionGroup>();
        // Unknown offsets never inherit the workstation timezone. Different cameras stay separate until explicitly corrected.
        foreach (var clock in members.Where(m => m.LocalTime.HasValue && m.Issue is null).GroupBy(m => m.UtcOffset.HasValue ? "UTC" : corrections?.ContainsKey(m.Camera) == true ? "corrected-local" : "local:" + m.Camera).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var ordered = clock.OrderBy(Effective).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id, StringComparer.Ordinal).ToArray();
            var current = new List<CaptureMember>(); DateTime? previous = null;
            foreach (var m in ordered) { var t = Effective(m); if (previous.HasValue && t - previous.Value > gap) { Add(current); current = []; } current.Add(m with { EffectiveTime = t }); previous = t; }
            Add(current);
            void Add(List<CaptureMember> batch) { if (batch.Count > 0) groups.Add(new(StableId(batch), $"场次 {groups.Count + 1}", batch.ToArray(), string.IsNullOrWhiteSpace(batch[0].Camera) || (clock.Key != "UTC" && (hasReliableOffset || hasDifferentCameras)))); }
        }
        foreach (var m in members.Where(m => !m.LocalTime.HasValue || m.Issue is not null).OrderBy(m => m.Path, StringComparer.Ordinal)) groups.Add(new(StableId([m]), "待确认", [m], true));
        return new(groups);
    }
    private static string StableId(IEnumerable<CaptureMember> members) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", members.Select(m => m.Id)))))[..24];
    public static IReadOnlyList<SessionGroup> Split(IReadOnlyList<SessionGroup> groups, int group, int member)
    {
        var selected = groups[group]; if (member <= 0 || member >= selected.Count) throw new ArgumentOutOfRangeException(nameof(member));
        var result = groups.ToList(); result[group] = selected with { Members = selected.Members.Take(member).ToArray() }; result.Insert(group + 1, selected with { Id = Guid.NewGuid().ToString("N"), Name = selected.Name + " · 分段", Members = selected.Members.Skip(member).ToArray() }); return result;
    }
    public static IReadOnlyList<SessionGroup> Merge(IReadOnlyList<SessionGroup> groups, int first)
    {
        if (first < 0 || first >= groups.Count - 1) throw new ArgumentOutOfRangeException(nameof(first));
        var result = groups.ToList(); var a = result[first]; var b = result[first + 1]; result[first] = a with { Members = a.Members.Concat(b.Members).ToArray(), NeedsReview = a.NeedsReview || b.NeedsReview }; result.RemoveAt(first + 1); return result;
    }
}
