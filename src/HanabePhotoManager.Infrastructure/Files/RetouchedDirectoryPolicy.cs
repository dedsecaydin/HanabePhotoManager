namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// 识别照片库日期目录下受保护的“修后”输出区域。
/// </summary>
public static class RetouchedDirectoryPolicy
{
    public const string DirectoryName = "修后";

    /// <summary>判断路径是否等于任一日期的修后目录或位于其内部。</summary>
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
