namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// Renumbers <c>JK%04d</c> files inside a category directory so that the
/// remaining files form a contiguous 1..N sequence after duplicates or other
/// files have been removed.
/// </summary>
public static class LibraryResequenceService
{
    /// <summary>
    /// Walks every category directory under the library root and renumbers
    /// <c>JK%04d</c> files to fill gaps left by deleted files.
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
                    ResequenceDirectory(categoryDir);
            }
        }
    }

    /// <summary>
    /// Renumbers all <c>JK%04d</c> files in a single category directory so
    /// that the sequence is contiguous starting from JK0001.
    /// Files sharing the same numeric base (e.g. JK0001.JPG and JK0001_02.XML)
    /// receive the same new sequence number with the same suffix pattern.
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
