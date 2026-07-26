using System.IO;

namespace HanabePhotoManager.App.Services;

public sealed class ImportSourceSettings
{
    public string Path { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IncludeSubdirectories { get; set; } = true;
    public bool AutoWatch { get; set; }
}

public readonly record struct ImportSourceAddResult(int Added, int Rejected);

public static class ImportSourcePolicy
{
    public static ImportSourceAddResult AddRange(IList<ImportSourceSettings> sources, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(candidates);
        var added = 0;
        var rejected = 0;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                rejected++;
                continue;
            }

            var normalized = Normalize(candidate);
            if (sources.Any(source => Overlaps(source.Path, normalized)))
            {
                rejected++;
                continue;
            }

            sources.Add(new ImportSourceSettings { Path = normalized });
            added++;
        }
        return new ImportSourceAddResult(added, rejected);
    }

    public static IReadOnlyList<string> EnabledScanPaths(IEnumerable<ImportSourceSettings> sources)
    {
        var result = new List<string>();
        foreach (var source in sources.Where(item => item.IsEnabled && !string.IsNullOrWhiteSpace(item.Path)))
        {
            var path = Normalize(source.Path);
            if (result.Any(existing => IsSameOrParent(existing, path))) continue;
            result.RemoveAll(existing => IsSameOrParent(path, existing));
            result.Add(path);
        }
        return result;
    }

    public static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static bool Overlaps(string left, string right) =>
        IsSameOrParent(Normalize(left), right) || IsSameOrParent(right, Normalize(left));

    private static bool IsSameOrParent(string parent, string child) =>
        string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
        child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
