namespace HanabePhotoManager.App.Navigation;

public static class NavigationOrderPolicy
{
    public static IReadOnlyList<string> Normalize(
        IEnumerable<string>? stored,
        IReadOnlyList<string> defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var allowed = defaults.ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();
        var migrated = stored ?? [];
        foreach (var key in migrated)
        {
            if (allowed.Contains(key) && !result.Contains(key, StringComparer.Ordinal))
            {
                result.Add(key);
            }
        }

        result.AddRange(defaults.Where(key => !result.Contains(key, StringComparer.Ordinal)));
        return result;
    }
}
