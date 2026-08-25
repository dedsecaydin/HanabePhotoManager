namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// 在文件删除后重新编号分类目录中的 <c>JK%04d</c> 文件，使剩余媒体保持连续序列。
/// </summary>
public static class LibraryResequenceService
{
    /// <summary>
    /// 遍历照片库的全部日期和分类目录，填补已删除文件留下的编号空洞；“修后”目录不会改名。
    /// </summary>
    public static void ResequenceLibrary(string libraryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        if (!Directory.Exists(libraryRoot)) return;

        foreach (var monthDir in Directory.GetDirectories(libraryRoot, "*月", SearchOption.TopDirectoryOnly))
        {
            foreach (var dateDir in Directory.GetDirectories(monthDir, "*?.??", SearchOption.TopDirectoryOnly))
            {
                foreach (var categoryDir in Directory.GetDirectories(dateDir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (string.Equals(
                            Path.GetFullPath(categoryDir),
                            Path.GetFullPath(Path.Combine(dateDir, RetouchedDirectoryPolicy.DirectoryName)),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ResequenceDirectory(categoryDir);
                }
            }
        }
    }

    /// <summary>
    /// 从 JK0001 开始重新编号单个分类目录。共享相同数字主干的文件会保留同组关系和后缀顺序。
    /// </summary>
    public static void ResequenceDirectory(string categoryDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryDir);
        if (!Directory.Exists(categoryDir)) return;

        var files = Directory.GetFiles(categoryDir, "JK*.*", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Stem = Path.GetFileNameWithoutExtension(path), Ext = Path.GetExtension(path).ToUpperInvariant() })
            .Where(f => f.Stem.Length >= 6 && f.Stem.StartsWith("JK", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => ExtractSequenceNumber(f.Stem))
            .ThenBy(f => f.Ext, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0) return;

        var tempPrefix = "__reseq_" + Guid.NewGuid().ToString("N") + "_";
        var renames = new List<(string From, string To)>();
        var sequence = 0;
        var prevBase = string.Empty;
        var extCount = 0;

        foreach (var file in files)
        {
            var stem = file.Stem;
            var ext = string.IsNullOrWhiteSpace(file.Ext) ? ".BIN" : file.Ext;

            var baseStem = stem;
            var underscoreIdx = stem.IndexOf('_');
            if (underscoreIdx >= 0)
                baseStem = stem[..underscoreIdx];

            if (baseStem != prevBase)
            {
                sequence++;
                extCount = 0;
                prevBase = baseStem;
            }

            extCount++;
            var newName = extCount == 1
                ? $"JK{sequence:0000}{ext}"
                : $"JK{sequence:0000}_{extCount:00}{ext}";
            var newPath = Path.Combine(categoryDir, newName);
            if (!string.Equals(file.Path, newPath, StringComparison.OrdinalIgnoreCase))
                renames.Add((file.Path, Path.Combine(categoryDir, tempPrefix + newName)));
        }

        if (renames.Count == 0) return;

        // 两阶段改名避免目标名称仍被尚未处理的旧文件占用。
        foreach (var (from, to) in renames)
        {
            try { File.Move(from, to); } catch (IOException) { }
        }

        foreach (var (_, tempTo) in renames)
        {
            var dir = Path.GetDirectoryName(tempTo)!;
            var finalName = Path.GetFileName(tempTo)[tempPrefix.Length..];
            try { File.Move(tempTo, Path.Combine(dir, finalName)); } catch (IOException) { }
        }
    }

    private static int ExtractSequenceNumber(string stem)
    {
        var digits = stem.Skip(2).TakeWhile(char.IsDigit).ToArray();
        return digits.Length > 0 && int.TryParse(digits, out var n) ? n : 0;
    }
}
