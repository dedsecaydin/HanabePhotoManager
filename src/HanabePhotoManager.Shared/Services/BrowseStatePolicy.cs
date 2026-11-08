namespace HanabePhotoManager.App.Services;

/// <summary>区分从普通图库还是人物筛选进入浏览页。</summary>
public enum BrowseEntryMode
{
    CrossLaunchRestore,
    SessionRestore,
    AlwaysAllDates
}

/// <summary>离开浏览页时保存的筛选、排序、缩放和滚动状态。</summary>
public sealed record BrowseSnapshot(
    string? DateKey,
    string Category,
    string SearchText,
    int SortIndex,
    double ThumbnailSize,
    string? ScrollAnchorPath)
{
    public static BrowseSnapshot AllDates { get; } = new(null, "全部", string.Empty, 0, 150, null);
}

/// <summary>无法恢复快照时使用的浏览默认值。</summary>
public sealed record BrowseDefaults(string RatingFilter, int SortIndex, double ThumbnailSize);

/// <summary>决定不同入口之间哪些浏览状态应恢复、清空或回退到默认值。</summary>
public sealed class BrowseStatePolicy
{
    public BrowseSnapshot ResolveOnEntry(
        BrowseEntryMode mode,
        BrowseSnapshot? persisted,
        BrowseSnapshot? session,
        BrowseDefaults? defaults = null)
    {
        var initial = defaults is null
            ? BrowseSnapshot.AllDates
            : BrowseSnapshot.AllDates with
            {
                SortIndex = defaults.SortIndex,
                ThumbnailSize = defaults.ThumbnailSize
            };
        return mode switch
        {
            BrowseEntryMode.CrossLaunchRestore => persisted ?? initial,
            BrowseEntryMode.SessionRestore => session ?? initial,
            BrowseEntryMode.AlwaysAllDates => (session ?? persisted ?? initial) with
            {
                DateKey = null,
                ScrollAnchorPath = null
            },
            _ => initial
        };
    }
}
