namespace HanabePhotoManager.App.Services;

public enum BrowseEntryMode
{
    CrossLaunchRestore,
    SessionRestore,
    AlwaysAllDates
}

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

public sealed record BrowseDefaults(string RatingFilter, int SortIndex, double ThumbnailSize);

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
