using System.Collections.Concurrent;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>
/// Scans the library directory for content-level duplicate detection.
/// Uses file size as a fast first-pass filter and SHA-256 for confirmation,
/// matching the strategy already used by <see cref="DestinationProbe"/>.
/// </summary>
public sealed class LibraryContentScanner
{
    private readonly IFileHasher _fileHasher;

    public LibraryContentScanner(IFileHasher fileHasher)
    {
        _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
    }

    /// <summary>
    /// Builds a map of file size → list of file paths for all files in the
    /// library root that match the given extensions.  This is the fast
    /// first-pass index used to avoid computing SHA-256 for every file.
    /// </summary>
    public async Task<Dictionary<long, List<string>>> BuildSizeMapAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        var sizeMap = new Dictionary<long, List<string>>();
        if (!Directory.Exists(libraryRoot))
            return sizeMap;

        var files = EnumerateLibraryFiles(libraryRoot, extensions);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long size;
            try { size = new FileInfo(path).Length; }
            catch (FileNotFoundException) { continue; }
            catch (IOException) { continue; }

            if (!sizeMap.TryGetValue(size, out var list))
            {
                list = new List<string>();
                sizeMap[size] = list;
            }
            list.Add(path);
        }

        return await Task.FromResult(sizeMap).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks whether the file at <paramref name="sourcePath"/> has a content
    /// duplicate anywhere in the library (identified via the size map).
    /// Returns the path of the first matching library file, or null.
    /// </summary>
    public async Task<string?> FindContentDuplicateAsync(
        string sourcePath,
        IReadOnlyDictionary<long, List<string>> sizeMap,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(sizeMap);

        if (!File.Exists(sourcePath))
            return null;

        long sourceSize;
        try { sourceSize = new FileInfo(sourcePath).Length; }
        catch (FileNotFoundException) { return null; }

        if (!sizeMap.TryGetValue(sourceSize, out var candidates) || candidates.Count == 0)
            return null;

        var sourceHash = await _fileHasher.ComputeSha256Async(sourcePath, cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(candidate, sourcePath, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                continue;

            string candidateHash;
            try
            {
                candidateHash = await _fileHasher.ComputeSha256Async(candidate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (FileNotFoundException) { continue; }
            catch (IOException) { continue; }

            if (string.Equals(sourceHash, candidateHash, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Scans the entire library and returns groups of files that have
    /// identical SHA-256 content.  Each group contains 2+ file paths.
    /// Uses file size as a first-pass filter for efficiency.
    /// </summary>
    public async Task<List<List<string>>> FindAllDuplicatesAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        var sizeMap = await BuildSizeMapAsync(libraryRoot, extensions, cancellationToken)
            .ConfigureAwait(false);

        var duplicateGroups = new List<List<string>>();
        var processed = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var (size, candidates) in sizeMap)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.Count < 2)
                continue;

            // Group candidates by hash.
            var byHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (processed.Contains(candidate))
                    continue;

                string hash;
                try
                {
                    hash = await _fileHasher.ComputeSha256Async(candidate, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (FileNotFoundException) { continue; }
                catch (IOException) { continue; }

                if (!byHash.TryGetValue(hash, out var group))
                {
                    group = new List<string>();
                    byHash[hash] = group;
                }
                group.Add(candidate);
            }

            foreach (var group in byHash.Values)
            {
                if (group.Count >= 2)
                {
                    duplicateGroups.Add(group);
                    foreach (var path in group)
                        processed.Add(path);
                }
            }
        }

        return duplicateGroups;
    }

    private static IEnumerable<string> EnumerateLibraryFiles(string root, IReadOnlySet<string> extensions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(current, "*", SearchOption.TopDirectoryOnly); }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }

            foreach (var entry in entries)
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        stack.Push(entry);
                        continue;
                    }
                }
                catch (UnauthorizedAccessException) { continue; }

                var ext = Path.GetExtension(entry);
                if (extensions.Contains(ext))
                {
                    var full = Path.GetFullPath(entry);
                    if (seen.Add(full))
                        yield return full;
                }
            }
        }
    }
}
