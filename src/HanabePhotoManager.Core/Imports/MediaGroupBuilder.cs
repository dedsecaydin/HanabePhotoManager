using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using HanabePhotoManager.Core;

namespace HanabePhotoManager.Core.Imports;

public sealed partial class MediaGroupBuilder(MediaClassifier classifier)
{
    private readonly MediaClassifier _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));

    public IReadOnlyList<MediaGroup> Build(IEnumerable<SourceMediaFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var normalizedFiles = ValidateAndNormalize(files);
        var sortedFiles = normalizedFiles
            .OrderBy(file => file.PathIdentity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.PathIdentity, StringComparer.Ordinal)
            .ToArray();
        var candidates = sortedFiles.Select(file => _classifier.Classify(file.Source)).ToArray();
        var sidecarIndex = BuildSidecarIndex(sortedFiles);
        var consumedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<NormalizedMediaGroup>();

        for (var index = 0; index < sortedFiles.Length; index++)
        {
            var primary = sortedFiles[index];
            var candidate = candidates[index];

            if (candidate.SuggestedCategory == MediaCategory.Video && IsPrimaryVideoFile(primary.PathIdentity))
            {
                groups.Add(new NormalizedMediaGroup(
                    BuildSonyGroup(primary, sidecarIndex, consumedPaths),
                    primary.PathIdentity));
            }
            else if (candidate.SuggestedCategory == MediaCategory.ActionVideo && IsPrimaryVideoFile(primary.PathIdentity))
            {
                groups.Add(new NormalizedMediaGroup(
                    BuildDjiGroup(primary, sidecarIndex, consumedPaths),
                    primary.PathIdentity));
            }
        }

        for (var index = 0; index < sortedFiles.Length; index++)
        {
            var file = sortedFiles[index];
            if (!consumedPaths.Add(file.PathIdentity))
            {
                continue;
            }

            groups.Add(new NormalizedMediaGroup(
                new MediaGroup(
                    LocalPathSyntax.GetFileNameWithoutExtension(file.PathIdentity),
                    candidates[index].SuggestedCategory,
                    file.Source,
                    Array.Empty<SourceMediaFile>()),
                file.PathIdentity));
        }

        var orderedGroups = groups
            .OrderBy(group => group.PathIdentity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.PathIdentity, StringComparer.Ordinal)
            .Select(group => group.Group)
            .ToArray();

        return new ReadOnlyCollection<MediaGroup>(orderedGroups);
    }

    private static IReadOnlyList<NormalizedMediaFile> ValidateAndNormalize(IEnumerable<SourceMediaFile> files)
    {
        var normalizedFiles = new List<NormalizedMediaFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file is null)
            {
                throw new ArgumentException("Input contains a null SourceMediaFile.", nameof(files));
            }

            if (string.IsNullOrWhiteSpace(file.FullPath))
            {
                throw new ArgumentException("SourceMediaFile FullPath cannot be null or whitespace.", nameof(files));
            }

            var pathIdentity = NormalizePathIdentity(file.FullPath);
            if (!paths.Add(pathIdentity))
            {
                throw new ArgumentException($"Duplicate FullPath '{file.FullPath}' is not allowed.", nameof(files));
            }

            normalizedFiles.Add(new NormalizedMediaFile(file, pathIdentity));
        }

        return normalizedFiles;
    }

    private static Dictionary<string, IReadOnlyList<NormalizedMediaFile>> BuildSidecarIndex(
        IReadOnlyList<NormalizedMediaFile> files)
    {
        var index = new Dictionary<string, List<NormalizedMediaFile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileName = LocalPathSyntax.GetFileName(file.PathIdentity);
            var sonyMatch = SonySidecarPattern().Match(fileName);
            if (sonyMatch.Success)
            {
                AddSidecar(index, CreateMediaKey(file.PathIdentity, sonyMatch.Groups[1].Value), file);
                continue;
            }

            var extension = Path.GetExtension(fileName);
            if (extension.Equals(".LRF", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".AAC", StringComparison.OrdinalIgnoreCase))
            {
                AddSidecar(index, CreateMediaKey(file.PathIdentity, LocalPathSyntax.GetFileNameWithoutExtension(fileName)), file);
            }
        }

        return index.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<NormalizedMediaFile>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AddSidecar(
        IDictionary<string, List<NormalizedMediaFile>> index,
        string key,
        NormalizedMediaFile file)
    {
        if (!index.TryGetValue(key, out var sidecars))
        {
            sidecars = new List<NormalizedMediaFile>();
            index.Add(key, sidecars);
        }

        sidecars.Add(file);
    }

    private static MediaGroup BuildSonyGroup(
        NormalizedMediaFile primary,
        IReadOnlyDictionary<string, IReadOnlyList<NormalizedMediaFile>> sidecarIndex,
        ISet<string> consumedPaths)
    {
        var key = LocalPathSyntax.GetFileNameWithoutExtension(primary.PathIdentity).ToUpperInvariant();
        var sidecars = ConsumeSidecars(primary, key, sidecarIndex, consumedPaths);

        return new MediaGroup(key, MediaCategory.Video, primary.Source, sidecars);
    }

    private static MediaGroup BuildDjiGroup(
        NormalizedMediaFile primary,
        IReadOnlyDictionary<string, IReadOnlyList<NormalizedMediaFile>> sidecarIndex,
        ISet<string> consumedPaths)
    {
        var key = LocalPathSyntax.GetFileNameWithoutExtension(primary.PathIdentity);
        var sidecars = ConsumeSidecars(primary, key, sidecarIndex, consumedPaths);

        return new MediaGroup(key, MediaCategory.ActionVideo, primary.Source, sidecars);
    }

    private static IReadOnlyList<SourceMediaFile> ConsumeSidecars(
        NormalizedMediaFile primary,
        string stem,
        IReadOnlyDictionary<string, IReadOnlyList<NormalizedMediaFile>> sidecarIndex,
        ISet<string> consumedPaths)
    {
        consumedPaths.Add(primary.PathIdentity);
        var key = CreateMediaKey(primary.PathIdentity, stem);
        var sidecars = sidecarIndex.GetValueOrDefault(key) ?? Array.Empty<NormalizedMediaFile>();
        foreach (var sidecar in sidecars)
        {
            consumedPaths.Add(sidecar.PathIdentity);
        }

        return sidecars.Select(sidecar => sidecar.Source).ToArray();
    }

    private static string CreateMediaKey(string pathIdentity, string stem)
    {
        var parentDirectory = LocalPathSyntax.GetDirectoryName(pathIdentity);
        return $"{parentDirectory}\0{stem}";
    }

    private static string NormalizePathIdentity(string fullPath)
    {
        if (!LocalPathSyntax.IsFullyQualified(fullPath))
        {
            throw new ArgumentException($"FullPath '{fullPath}' must be fully qualified.", nameof(fullPath));
        }

        return LocalPathSyntax.NormalizeIdentity(fullPath);
    }

    private static bool IsPrimaryVideoFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".MP4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".MOV", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".MTS", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".M2TS", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^(C[0-9]{4})M[0-9]{2}\.XML$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SonySidecarPattern();

    private sealed record NormalizedMediaFile(SourceMediaFile Source, string PathIdentity);

    private sealed record NormalizedMediaGroup(MediaGroup Group, string PathIdentity);
}
