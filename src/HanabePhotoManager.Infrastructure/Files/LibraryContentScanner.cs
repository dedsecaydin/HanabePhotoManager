using System.Collections.Concurrent;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        var sizeMap = new Dictionary<long, List<string>>();
        if (!Directory.Exists(libraryRoot))
            return sizeMap;

        var files = EnumerateLibraryFiles(libraryRoot, extensions).ToArray();
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = files[index];
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

            // 枚举阶段：0% → 40%
            progress?.Report(files.Length == 0 ? 40d : index * 40d / files.Length);
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
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        var sizeMap = await BuildSizeMapAsync(libraryRoot, extensions, cancellationToken, progress)
            .ConfigureAwait(false);

        var duplicateGroups = new List<List<string>>();
        var processed = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var hashTasks = sizeMap
            .Where(pair => pair.Value.Count >= 2)
            .SelectMany(pair => pair.Value)
            .Distinct(OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
        var hashedIndex = 0;
        var totalCandidates = Math.Max(1, hashTasks.Length);

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

                hashedIndex++;
                // 哈希比对阶段：40% → 100%
                progress?.Report(40d + hashedIndex * 60d / totalCandidates);

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

        progress?.Report(100d);
        return duplicateGroups;
    }

    /// <summary>
    /// Maximum Hamming distance between two 64-bit average hashes for the
    /// corresponding images to be considered visually similar (a near-duplicate).
    /// </summary>
    public const int DuplicateHammingThreshold = 8;

    /// <summary>
    /// Scans the entire library for visually similar images (re-encoded, resized or
    /// re-compressed copies of the same photo) using a perceptual average hash.
    /// This catches duplicates that an exact SHA-256 comparison misses. Files whose
    /// paths appear in <paramref name="excludePaths"/> are skipped so that groups
    /// already confirmed by an exact content match are not reported twice.
    /// </summary>
    public async Task<List<List<string>>> FindVisualDuplicatesAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        IReadOnlyCollection<string>? excludePaths,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        // Perceptual hashing only applies to raster images, not video containers.
        var imageExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        imageExtensions.Remove(".mp4");
        imageExtensions.Remove(".mov");

        var paths = EnumerateLibraryFiles(libraryRoot, imageExtensions).ToList();
        if (excludePaths is not null && excludePaths.Count > 0)
        {
            var excluded = new HashSet<string>(excludePaths, StringComparer.OrdinalIgnoreCase);
            paths.RemoveAll(path => excluded.Contains(path));
        }

        if (paths.Count < 2)
            return new List<List<string>>();

        var hashes = new List<(string Path, ulong Hash)>(paths.Count);
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = paths[index];
            try
            {
                hashes.Add((path, ComputeAverageHash(path)));
            }
            catch (FileNotFoundException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (InvalidImageContentException) { }
            catch (ImageFormatException) { }

            // 视觉指纹检测阶段：0% → 100%（VM 层会缩放到 80% → 100%）
            progress?.Report(index * 100d / paths.Count);

            // Yield periodically so a large library scan stays responsive.
            if ((hashes.Count & 31) == 0)
                await Task.Yield();
        }

        // Bucket by the top 16 bits of the hash so we only compare visually
        // close candidates instead of doing an O(n^2) scan across the whole library.
        var buckets = new Dictionary<uint, List<int>>();
        for (var index = 0; index < hashes.Count; index++)
        {
            var key = (uint)(hashes[index].Hash >> 48);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<int>();
                buckets[key] = list;
            }

            list.Add(index);
        }

        var visited = new bool[hashes.Count];
        var groups = new List<List<string>>();
        for (var i = 0; i < hashes.Count; i++)
        {
            if (visited[i])
                continue;

            var group = new List<string> { hashes[i].Path };
            visited[i] = true;

            var top = (int)(hashes[i].Hash >> 48);
            for (var bucketKey = top - 2; bucketKey <= top + 2; bucketKey++)
            {
                if (bucketKey < 0 || !buckets.TryGetValue((uint)bucketKey, out var bucket))
                    continue;

                foreach (var j in bucket)
                {
                    if (visited[j] || j == i)
                        continue;

                    if (HammingDistance(hashes[i].Hash, hashes[j].Hash) <= DuplicateHammingThreshold)
                    {
                        group.Add(hashes[j].Path);
                        visited[j] = true;
                    }
                }
            }

            if (group.Count >= 2)
                groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Computes a 64-bit average perceptual hash: resize to 8x8 grayscale, threshold
    /// each pixel against the mean, and pack the bits. Identical or near-identical
    /// images produce hashes with a small Hamming distance.
    /// </summary>
    private static ulong ComputeAverageHash(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        image.Mutate(ctx => ctx.Resize(8, 8, KnownResamplers.Box).Grayscale());

        var values = new byte[64];
        long sum = 0;
        for (var i = 0; i < 64; i++)
        {
            var pixel = image[i % 8, i / 8];
            values[i] = pixel.R;
            sum += pixel.R;
        }

        var mean = sum / 64d;
        ulong hash = 0;
        for (var i = 0; i < 64; i++)
        {
            if (values[i] >= mean)
                hash |= 1UL << i;
        }

        return hash;
    }

    private static int HammingDistance(ulong left, ulong right)
    {
        var diff = left ^ right;
        var count = 0;
        while (diff != 0)
        {
            diff &= diff - 1;
            count++;
        }

        return count;
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
