using System.IO;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.App.Services;

public sealed class RetouchedMediaIndex
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".psd", ".psb"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RasterExtensions = new(
        [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex ExportSuffix = new(
        @"(?:-恢复的|_ExHiRes|_noeffect|[-_](?:修后|edited|edit|final|retouched))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public RetouchedMediaSnapshot Build(
        string dateDirectory,
        IReadOnlyList<string> originalPaths,
        IReadOnlyList<string>? enumeratedOutputs = null)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var retouchedDirectory = Path.Combine(dateDirectory, "修后");
        if (enumeratedOutputs is null && !Directory.Exists(retouchedDirectory))
        {
            return new RetouchedMediaSnapshot(map, []);
        }

        var outputs = (enumeratedOutputs ?? Directory.EnumerateFiles(
                retouchedDirectory, "*", SearchOption.AllDirectories))
            .Where(IsSupported)
            .ToArray();
        var outputsByStem = outputs
            .GroupBy(path => NormalizeStem(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, SelectPreferred, StringComparer.OrdinalIgnoreCase);
        var originalStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var original in originalPaths)
        {
            var stem = NormalizeStem(Path.GetFileNameWithoutExtension(original));
            if (stem.Length == 0) continue;
            originalStems.Add(stem);
            if (outputsByStem.TryGetValue(stem, out var output)) map[original] = output;
        }

        var standalone = outputs
            .Where(path => !originalStems.Contains(NormalizeStem(Path.GetFileNameWithoutExtension(path))))
            .ToArray();
        return new RetouchedMediaSnapshot(map, standalone);
    }

    public static string NormalizeStem(string stem) =>
        string.IsNullOrWhiteSpace(stem) ? string.Empty : ExportSuffix.Replace(stem.Trim(), string.Empty);

    private static bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private static string SelectPreferred(IEnumerable<string> paths) => paths
        .OrderByDescending(path => RasterExtensions.Contains(Path.GetExtension(path)))
        .ThenByDescending(path =>
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        })
        .First();
}

public sealed record RetouchedMediaSnapshot(
    IReadOnlyDictionary<string, string> RetouchedByOriginal,
    IReadOnlyList<string> StandaloneRetouchedFiles);
