using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed class LibraryDirectoryInitializer
{
    public static readonly IReadOnlyList<string> CategoryFolders = Array.AsReadOnly(
    [
        "RAW生图",
        "JPG生图",
        "修后",
        "视频",
        "action视频",
        "素材"
    ]);

    public void EnsureDateTree(string root, LibraryDate date)
        => EnsureDateTree(root, date.RelativePath);

    public void EnsureDateTree(string root, string dateRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateRelativePath);
        if (Path.IsPathFullyQualified(dateRelativePath))
            throw new ArgumentException("Date path must be relative.", nameof(dateRelativePath));

        var normalizedRoot = Path.GetFullPath(root);
        normalizedRoot = Path.TrimEndingDirectorySeparator(normalizedRoot);

        foreach (var categoryFolder in CategoryFolders)
        {
            var directory = Path.GetFullPath(Path.Combine(normalizedRoot, dateRelativePath, categoryFolder));
            if (!IsWithinRoot(normalizedRoot, directory))
            {
                throw new InvalidOperationException($"Resolved date directory escapes the library root: {directory}");
            }

            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsWithinRoot(string normalizedRoot, string candidate)
    {
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
