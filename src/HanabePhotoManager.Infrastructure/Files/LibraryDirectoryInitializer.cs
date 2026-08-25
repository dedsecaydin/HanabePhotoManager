using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>按照片库日期和固定分类创建安全的目录树。</summary>
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

    /// <summary>确保指定自然日下的全部分类目录存在且没有逃逸照片库根目录。</summary>
    public void EnsureDateTree(string root, LibraryDate date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var normalizedRoot = Path.GetFullPath(root);
        normalizedRoot = Path.TrimEndingDirectorySeparator(normalizedRoot);

        foreach (var categoryFolder in CategoryFolders)
        {
            var directory = Path.GetFullPath(Path.Combine(normalizedRoot, date.RelativePath, categoryFolder));
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
