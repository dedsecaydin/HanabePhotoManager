using System.IO;
using System.Text.Json;

namespace HanabePhotoManager.App.Services;

/// <summary>
/// Fully local face search using the selected embedding engine. No image or face
/// feature leaves the computer. The library cache is keyed by path, size and
/// modification time so repeat searches do not decode the whole library again.
/// </summary>
public sealed class FaceSearchService : IFaceSearchService
{
    private static readonly HashSet<string> SearchableExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _cachePath;
    private readonly Func<IFaceSearchEmbeddingEngine> _engineFactory;

    public FaceSearchService(Func<IFaceSearchEmbeddingEngine> engineFactory, string? cachePath = null)
    {
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _cachePath = cachePath ?? Path.Combine(AppDataPaths.Root, "FaceSearch", "face-features.json");
    }

    public async Task<FaceReference> CreateReferenceAsync(string imagePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var engine = _engineFactory();
            var embeddings = engine.ExtractEmbeddings(imagePath, cancellationToken);
            if (embeddings.Count == 0)
            {
                throw new InvalidOperationException("参考图中没有检测到清晰的正面人脸，请换一张脸更大、光线更好的照片。");
            }

            // The detector returns high confidence/larger faces first. For a
            // reference image the first face is the intended subject.
            return new FaceReference(imagePath, embeddings[0]);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FaceSearchMatch>> SearchAsync(
        FaceReference reference,
        string libraryRoot,
        double minimumSimilarity,
        IProgress<FaceSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!Directory.Exists(libraryRoot))
        {
            throw new DirectoryNotFoundException("照片库路径不存在或当前无法访问。");
        }

        return await Task.Run(() => SearchCore(
            reference, libraryRoot, minimumSimilarity, progress, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<FaceSearchMatch> SearchCore(
        FaceReference reference,
        string libraryRoot,
        double minimumSimilarity,
        IProgress<FaceSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = EnumerateImages(libraryRoot, cancellationToken).ToArray();
        var cache = LoadCache();
        var matches = new List<FaceSearchMatch>();
        var cacheChanged = false;

        using var engine = _engineFactory();
        try
        {
            for (var index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = files[index];
                var fingerprint = engine.CacheIdentity + ":" + CreateFingerprint(path);
                IReadOnlyList<float[]> embeddings;
                var fromCache = cache.TryGetValue(path, out var entry) && entry.Fingerprint == fingerprint;

                if (fromCache)
                {
                    embeddings = entry!.Embeddings;
                }
                else
                {
                    embeddings = engine.ExtractEmbeddings(path, cancellationToken);
                    cache[path] = new FaceCacheEntry(fingerprint, embeddings.Select(v => v.ToArray()).ToArray());
                    cacheChanged = true;
                }

                var best = embeddings.Count == 0
                    ? 0d
                    : embeddings.Max(candidate => CosineSimilarity(reference.Embedding, candidate));
                var didMatch = best >= minimumSimilarity;
                if (didMatch)
                {
                    matches.Add(new FaceSearchMatch(path, best, embeddings.Count));
                }

                // Cached scans can process thousands of files per second. Throttling
                // avoids flooding the WPF dispatcher and keeps stop/navigation responsive.
                if (!fromCache || didMatch || index % 12 == 0 || index == files.Length - 1)
                {
                    progress?.Report(new FaceSearchProgress(
                        index + 1, files.Length, matches.Count, fromCache, Path.GetFileName(path)));
                }
            }
        }
        finally
        {
            // A cancelled first scan still keeps everything completed so far.
            if (cacheChanged) SaveCache(cache, libraryRoot);
        }

        return matches
            .OrderByDescending(item => item.Similarity)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item.Path))
            .Take(300)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateImages(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<string> childDirectories = [];
            IEnumerable<string> childFiles = [];
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory);
                childFiles = Directory.EnumerateFiles(directory);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            foreach (var child in childDirectories)
            {
                if (!string.Equals(Path.GetFileName(child), ".hanabe", StringComparison.OrdinalIgnoreCase))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in childFiles)
            {
                if (SearchableExtensions.Contains(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
        }
    }

    private Dictionary<string, FaceCacheEntry> LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, FaceCacheEntry>>(File.ReadAllText(_cachePath))
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCache(Dictionary<string, FaceCacheEntry> cache, string libraryRoot)
    {
        try
        {
            var fullRoot = Path.GetFullPath(libraryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var compact = cache
                .Where(pair => pair.Key.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var temporary = _cachePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(compact));
            File.Move(temporary, _cachePath, true);
        }
        catch
        {
            // A cache failure must never make face search fail.
        }
    }

    private static string CreateFingerprint(string path)
    {
        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static double CosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        if (first.Count == 0 || first.Count != second.Count) return 0;
        double dot = 0, firstLength = 0, secondLength = 0;
        for (var index = 0; index < first.Count; index++)
        {
            dot += first[index] * second[index];
            firstLength += first[index] * first[index];
            secondLength += second[index] * second[index];
        }

        var denominator = Math.Sqrt(firstLength) * Math.Sqrt(secondLength);
        return denominator <= 0.000001 ? 0 : Math.Clamp(dot / denominator, -1, 1);
    }

    private sealed record FaceCacheEntry(string Fingerprint, float[][] Embeddings);
}

