namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// Defines the immutable retouched output directory for a library date.
/// </summary>
public static class RetouchedDirectoryPolicy
{
    public const string DirectoryName = "修后";

    public static bool IsReadOnlyRetouchedPath(string libraryRoot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        foreach (var monthDir in Directory.EnumerateDirectories(libraryRoot, "*月", SearchOption.TopDirectoryOnly))
        {
            foreach (var dateDir in Directory.EnumerateDirectories(monthDir, "??.??", SearchOption.TopDirectoryOnly))
            {
                var retouchedDir = Path.GetFullPath(Path.Combine(dateDir, DirectoryName));
                if (IsSameOrChildPath(retouchedDir, fullPath))
                    return true;
            }
        }

        return false;
    }

    private static bool IsSameOrChildPath(string parent, string candidate)
    {
        var normalizedParent = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.TrimEndingDirectorySeparator(parent), Path.TrimEndingDirectorySeparator(candidate), StringComparison.OrdinalIgnoreCase);
    }
}
