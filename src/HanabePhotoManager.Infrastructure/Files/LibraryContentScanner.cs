using System.Collections.Concurrent;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed record DuplicateScanProgress(
    string Stage,
    int Processed,
    int Total,
    string? CurrentPath,
    int GroupsFound);

/// <summary>
/// 扫描照片库并检测内容重复和视觉近似文件。精确查重先按大小分桶，再使用 SHA-256 确认，
/// 与 <see cref="DestinationProbe"/> 的冲突判断策略保持一致。
/// </summary>
public sealed class LibraryContentScanner
{
    private readonly IFileHasher _fileHasher;

    /// <summary>创建使用指定哈希实现的内容扫描器。</summary>
    public LibraryContentScanner(IFileHasher fileHasher)
    {
        _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
    }

    /// <summary>
    /// 为照片库中匹配扩展名的文件构建“文件大小 → 路径列表”映射，避免对唯一大小文件计算哈希。
    /// </summary>
    public async Task<Dictionary<long, List<string>>> BuildSizeMapAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        IProgress<DuplicateScanProgress>? detailProgress = null)
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
            detailProgress?.Report(new("建立文件清单", index + 1, files.Length, path, 0));
        }

        return await Task.FromResult(sizeMap).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用大小映射检查源文件是否在照片库中存在内容副本；返回第一个 SHA-256 相同的路径。
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
    /// 扫描整个照片库并返回 SHA-256 完全相同的文件组；每组至少包含两个路径。
    /// </summary>
    public async Task<List<List<string>>> FindAllDuplicatesAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        IProgress<DuplicateScanProgress>? detailProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        var sizeMap = await BuildSizeMapAsync(libraryRoot, extensions, cancellationToken, progress, detailProgress)
            .ConfigureAwait(false);

        var hashTasks = sizeMap
            .Where(pair => pair.Value.Count >= 2)
            .SelectMany(pair => pair.Value.Select(path => (Size: pair.Key, Path: path)))
            .DistinctBy(item => item.Path, OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
        var totalCandidates = Math.Max(1, hashTasks.Length);
        var hashedIndex = 0;
        var hashedFiles = new ConcurrentBag<(long Size, string Path, string Hash)>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            // 少量并行可显著利用 SSD 和 SHA 硬件加速，同时避免大量并发读拖慢机械盘。
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 4)
        };
        await Parallel.ForEachAsync(hashTasks, parallelOptions, async (item, token) =>
        {
            try
            {
                var hash = await _fileHasher.ComputeSha256Async(item.Path, token).ConfigureAwait(false);
                hashedFiles.Add((item.Size, item.Path, hash));
            }
            catch (FileNotFoundException) { }
            catch (IOException) { }
            finally
            {
                var completed = Interlocked.Increment(ref hashedIndex);
                progress?.Report(40d + completed * 60d / totalCandidates);
                detailProgress?.Report(new("SHA-256 并行比对", completed, totalCandidates, item.Path, 0));
            }
        }).ConfigureAwait(false);

        var duplicateGroups = hashedFiles
            .GroupBy(item => (item.Size, item.Hash))
            .Where(group => group.Count() >= 2)
            .Select(group => group.Select(item => item.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList())
            .ToList();
        if (duplicateGroups.Count > 0)
            detailProgress?.Report(new("SHA-256 并行比对", hashedIndex, totalCandidates, duplicateGroups[^1][0], duplicateGroups.Count));

        progress?.Report(100d);
        return duplicateGroups;
    }

    /// <summary>
    /// 两个 64 位平均哈希被视为视觉近似时允许的最大汉明距离。
    /// </summary>
    public const int DuplicateHammingThreshold = 8;

    /// <summary>
    /// 使用感知平均哈希查找重编码、缩放或重新压缩后的近似图片。<paramref name="excludePaths"/>
    /// 中已由精确查重确认的路径会被排除，避免重复报告同一组。
    /// </summary>
    public async Task<List<List<string>>> FindVisualDuplicatesAsync(
        string libraryRoot,
        IReadOnlySet<string> extensions,
        IReadOnlyCollection<string>? excludePaths,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        IProgress<DuplicateScanProgress>? detailProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentNullException.ThrowIfNull(extensions);

        // 感知哈希只适用于可解码的栅格图片，视频容器由精确内容查重处理。
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
            detailProgress?.Report(new("视觉指纹生成", index + 1, paths.Count, path, 0));

            // 周期性让出执行权，防止大图库视觉扫描长期占满调用线程。
            if ((hashes.Count & 31) == 0)
                await Task.Yield();
        }

        // 按哈希高 16 位分桶，避免对整个图库执行 O(n²) 两两比较。
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
            {
                groups.Add(group);
                detailProgress?.Report(new("视觉近似分组", i + 1, hashes.Count, hashes[i].Path, groups.Count));
            }
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
