namespace HanabePhotoManager.App.Search;

public static class SemanticBrowseRanking
{
    public static IEnumerable<T> Apply<T>(
        IEnumerable<T> source,
        Func<T, string> pathSelector,
        IReadOnlyList<string>? rankedPaths)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pathSelector);

        if (rankedPaths is null)
        {
            return source;
        }

        var ranks = rankedPaths
            .Select((path, rank) => (Path: path, Rank: rank))
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Min(item => item.Rank), StringComparer.OrdinalIgnoreCase);

        return source
            .Where(item => ranks.ContainsKey(pathSelector(item)))
            .OrderBy(item => ranks[pathSelector(item)]);
    }
}
