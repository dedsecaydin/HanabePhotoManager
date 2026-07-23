namespace HanabePhotoManager.Core.Imports;

public sealed class ImportPlanBuilder(IDestinationProbe destinationProbe)
{
    private static readonly IReadOnlyDictionary<MediaCategory, string> CategoryFolders = new Dictionary<MediaCategory, string>
    {
        [MediaCategory.Raw] = "RAW生图",
        [MediaCategory.Jpeg] = "JPG生图",
        [MediaCategory.Edited] = "修后",
        [MediaCategory.Video] = "视频",
        [MediaCategory.ActionVideo] = "action视频",
        [MediaCategory.Material] = "素材"
    };

    private readonly IDestinationProbe _destinationProbe = destinationProbe ?? throw new ArgumentNullException(nameof(destinationProbe));

    public async Task<ImportPlan> BuildAsync(
        string root,
        LibraryDate date,
        TransferMode mode,
        IEnumerable<MediaGroup> groups,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(groups);

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Library root cannot be null or whitespace.", nameof(root));
        }

        var inputGroups = groups.ToArray();
        ValidateGroups(inputGroups, nameof(groups));

        var sequenceByGroup = BuildSequenceMap(root, date, inputGroups);
        var items = new List<ImportPlanItem>();
        var plannedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in inputGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var categoryFolder = CategoryFolders[group.Category];
            var plannedFiles = new List<PlannedFile>();
            var sequenceName = sequenceByGroup[group];
            var extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in EnumerateGroupFiles(group))
            {
                var destinationFileName = BuildRenamedFileName(sequenceName, source.FullPath, extensionCounts);
                var destination = Path.Combine(root, date.RelativePath, categoryFolder, destinationFileName);
                var normalizedDestination = NormalizeDestinationIdentity(destination);
                var conflict = plannedDestinations.Add(normalizedDestination)
                    ? await _destinationProbe.CheckAsync(source, destination, cancellationToken).ConfigureAwait(false)
                    : ConflictKind.SameNameDifferentContent;

                plannedFiles.Add(new PlannedFile(
                    source,
                    destination,
                    destination + ".hanabe-part",
                    conflict));
            }

            items.Add(new ImportPlanItem(
                Guid.NewGuid(),
                group,
                plannedFiles,
                AggregateConflict(plannedFiles),
                ImportItemState.Planned));
        }

        return new ImportPlan(root, date, mode, items);
    }

    private static void ValidateGroups(IReadOnlyList<MediaGroup> groups, string parameterName)
    {
        foreach (var group in groups)
        {
            if (group is null)
            {
                throw new ArgumentException("Groups cannot contain a null MediaGroup.", parameterName);
            }

            if (!CategoryFolders.ContainsKey(group.Category))
            {
                throw new ArgumentException(
                    $"Media group category '{group.Category}' is not a concrete import category.",
                    parameterName);
            }
        }
    }

    private static Dictionary<MediaGroup, string> BuildSequenceMap(string root, LibraryDate date, IReadOnlyList<MediaGroup> groups)
    {
        var result = new Dictionary<MediaGroup, string>();
        foreach (var categoryGroup in groups.GroupBy(group => group.Category))
        {
            var categoryFolder = CategoryFolders[categoryGroup.Key];
            var next = FindNextSequence(Path.Combine(root, date.RelativePath, categoryFolder));
            foreach (var group in categoryGroup
                         .OrderBy(group => Path.GetFileNameWithoutExtension(group.Primary.FullPath), NaturalStringComparer.OrdinalIgnoreCase)
                         .ThenBy(group => group.Primary.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                result[group] = $"JK{next:0000}";
                next++;
            }
        }

        return result;
    }

    private static int FindNextSequence(string categoryDirectory)
    {
        if (!Directory.Exists(categoryDirectory))
        {
            return 1;
        }

        var max = 0;
        foreach (var file in Directory.EnumerateFiles(categoryDirectory, "JK*.*", SearchOption.TopDirectoryOnly))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Length < 6 || !stem.StartsWith("JK", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var digits = new string(stem.Skip(2).TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var number))
            {
                max = Math.Max(max, number);
            }
        }

        return max + 1;
    }

    private static string BuildRenamedFileName(string sequenceName, string sourcePath, IDictionary<string, int> extensionCounts)
    {
        var extension = Path.GetExtension(sourcePath).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".BIN";
        }

        var count = extensionCounts.TryGetValue(extension, out var current) ? current + 1 : 1;
        extensionCounts[extension] = count;
        return count == 1
            ? sequenceName + extension
            : $"{sequenceName}_{count:00}{extension}";
    }

    private static string NormalizeDestinationIdentity(string destination)
    {
        var normalized = Path.GetFullPath(destination)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(normalized);
    }

    private static IEnumerable<SourceMediaFile> EnumerateGroupFiles(MediaGroup group)
    {
        yield return group.Primary;
        foreach (var sidecar in group.Sidecars)
        {
            yield return sidecar;
        }
    }

    private static ConflictKind AggregateConflict(IReadOnlyCollection<PlannedFile> files)
    {
        if (files.Any(file => file.Conflict == ConflictKind.SameNameDifferentContent))
        {
            return ConflictKind.SameNameDifferentContent;
        }

        return files.All(file => file.Conflict == ConflictKind.Identical)
            ? ConflictKind.Identical
            : ConflictKind.None;
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer OrdinalIgnoreCase { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var ix = 0;
            var iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                var cx = x[ix];
                var cy = y[iy];
                if (char.IsDigit(cx) && char.IsDigit(cy))
                {
                    var sx = ix;
                    var sy = iy;
                    while (ix < x.Length && char.IsDigit(x[ix]))
                    {
                        ix++;
                    }

                    while (iy < y.Length && char.IsDigit(y[iy]))
                    {
                        iy++;
                    }

                    var nx = x.AsSpan(sx, ix - sx).TrimStart('0');
                    var ny = y.AsSpan(sy, iy - sy).TrimStart('0');
                    var lengthCompare = nx.Length.CompareTo(ny.Length);
                    if (lengthCompare != 0)
                    {
                        return lengthCompare;
                    }

                    var numericCompare = string.Compare(nx.ToString(), ny.ToString(), StringComparison.Ordinal);
                    if (numericCompare != 0)
                    {
                        return numericCompare;
                    }

                    continue;
                }

                var charCompare = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                ix++;
                iy++;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
