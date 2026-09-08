using System.IO;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace HanabePhotoManager.App.Services;

/// <summary>扫描照片库、复用人脸检查点并生成可浏览的人物相册快照。</summary>
public sealed class PeopleAlbumService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string? _storePath;
    private readonly ILocalFaceEmbeddingService _embeddingService;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate;

    public PeopleAlbumService(string? storePath = null, ILocalFaceEmbeddingService? embeddingService = null)
    {
        _storePath = storePath;
        _embeddingService = embeddingService ?? new LocalFaceEmbeddingService();
        _gate = Gates.GetOrAdd(Path.GetFullPath(ResolveStorePath()), _ => new SemaphoreSlim(1, 1));
    }

    public FaceModelIdentity ModelIdentity => _embeddingService.ModelIdentity;

    public async Task<PeopleAlbumSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<PeopleAlbumSnapshot> ScanAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
        => await ScanAsync(paths, progress: null, cancellationToken).ConfigureAwait(false);

    public async Task<PeopleAlbumSnapshot> ScanAsync(
        IEnumerable<string> paths,
        IProgress<PeopleScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            snapshot.Albums ??= [];
            snapshot.RemovedPhotos ??= new(StringComparer.OrdinalIgnoreCase);
            var scanPaths = paths.Where(File.Exists).Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var rescanned = scanPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var album in snapshot.Albums)
                album.PhotoPaths.RemoveAll(path => rescanned.Contains(Path.GetFullPath(path))
                    && !album.ManualPhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase));

            var detectedFaces = 0;
            const int batchSize = 16;
            progress?.Report(new PeopleScanProgress(
                0, scanPaths.Length, 0, snapshot.Albums.Count, CreateProgressAlbums(snapshot.Albums)));
            for (var batchStart = 0; batchStart < scanPaths.Length; batchStart += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = scanPaths.Skip(batchStart).Take(batchSize).ToArray();
                var batchFaces = await _embeddingService.DetectBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                detectedFaces += batchFaces.Count;
                foreach (var path in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var face in batchFaces.Where(face =>
                                 string.Equals(face.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var album = FindBestAlbum(snapshot.Albums, face.Embedding, snapshot.MatchThreshold);
                        var created = album is null;
                        if (album is null)
                        {
                            album = new PersonAlbum
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                Name = NextDefaultName(snapshot.Albums),
                                MatchCentroids = [face.Embedding.ToArray()]
                            };
                            snapshot.Albums.Add(album);
                        }
                        else if (album.MatchCentroids.Count < 8
                                 && album.MatchCentroids.All(centroid => Cosine(centroid, face.Embedding) < 0.92))
                        {
                            album.MatchCentroids.Add(face.Embedding.ToArray());
                        }
                        if (!IsRemoved(snapshot, album.Id, path)
                            && !album.PhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                            album.PhotoPaths.Add(path);
                        if (created || string.IsNullOrWhiteSpace(album.CoverPath) || !File.Exists(album.CoverPath))
                            album.CoverPath = CreateFaceCover(face, album.Id);
                    }
                }
                progress?.Report(new PeopleScanProgress(
                    Math.Min(batchStart + batch.Length, scanPaths.Length),
                    scanPaths.Length,
                    detectedFaces,
                    snapshot.Albums.Count,
                    CreateProgressAlbums(snapshot.Albums)));
            }

            snapshot.Albums.RemoveAll(album => album.PhotoPaths.Count == 0 && string.IsNullOrWhiteSpace(album.Name));
            snapshot.Undo = null; // A completed scan establishes a new baseline.
            await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return snapshot;
        }
        finally { _gate.Release(); }
    }

    public Task RenameAsync(string albumId, string name, CancellationToken cancellationToken) =>
        MutateAsync(snapshot =>
        {
            var album = snapshot.Albums.FirstOrDefault(item => item.Id == albumId);
            if (album is not null && !string.IsNullOrWhiteSpace(name)) album.Name = name.Trim();
        }, cancellationToken);

    public Task RemovePhotoAsync(string albumId, string path, CancellationToken cancellationToken) =>
        MutateAsync(snapshot =>
        {
            var fullPath = Path.GetFullPath(path);
            var album = snapshot.Albums.FirstOrDefault(item => item.Id == albumId);
            if (album is null || !album.PhotoPaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) return;
            if (!snapshot.RemovedPhotos.TryGetValue(albumId, out var removed))
                snapshot.RemovedPhotos[albumId] = removed = [];
            if (!removed.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) removed.Add(fullPath);
            snapshot.Albums.FirstOrDefault(item => item.Id == albumId)?.PhotoPaths
                .RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
            album.ManualPhotoPaths.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
            album.CoverPath = album.PhotoPaths.FirstOrDefault() ?? string.Empty;
        }, cancellationToken);

    public Task MergeAsync(string targetId, string sourceId, CancellationToken cancellationToken) =>
        MutateAsync(snapshot =>
        {
            var target = snapshot.Albums.FirstOrDefault(item => item.Id == targetId);
            var source = snapshot.Albums.FirstOrDefault(item => item.Id == sourceId);
            if (target is null || source is null || ReferenceEquals(target, source)) return;
            foreach (var centroid in source.MatchCentroids) target.MatchCentroids.Add(centroid);
            foreach (var path in source.ManualPhotoPaths)
                if (!target.ManualPhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) target.ManualPhotoPaths.Add(path);
            foreach (var path in source.PhotoPaths)
                if (!target.PhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) target.PhotoPaths.Add(path);
            if (snapshot.RemovedPhotos.TryGetValue(sourceId, out var removed))
            {
                if (!snapshot.RemovedPhotos.TryGetValue(targetId, out var targetRemoved))
                    snapshot.RemovedPhotos[targetId] = targetRemoved = [];
                foreach (var path in removed)
                    if (!targetRemoved.Contains(path, StringComparer.OrdinalIgnoreCase)) targetRemoved.Add(path);
                snapshot.RemovedPhotos.Remove(sourceId);
            }
            snapshot.Albums.Remove(source);
        }, cancellationToken);

    /// <summary>Move references into a manual album without touching media or retraining from an ambiguous photo.</summary>
    public async Task<string> SplitAsync(string sourceId, IEnumerable<string> paths, string name, CancellationToken cancellationToken)
    {
        var selected = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var id = Guid.NewGuid().ToString("N");
        await MutateAsync(snapshot =>
        {
            var source = snapshot.Albums.FirstOrDefault(album => album.Id == sourceId)
                ?? throw new InvalidOperationException("人物已不存在，请刷新后重试。");
            if (selected.Length == 0 || selected.Any(path => !source.PhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException("请选择当前人物中的照片。");
            snapshot.Albums.Add(new PersonAlbum
            {
                Id = id, Name = string.IsNullOrWhiteSpace(name) ? NextDefaultName(snapshot.Albums) : name.Trim(),
                CoverPath = selected[0], PhotoPaths = selected.ToList(), ManualPhotoPaths = selected.ToList()
            });
            if (!snapshot.RemovedPhotos.TryGetValue(sourceId, out var removed))
                snapshot.RemovedPhotos[sourceId] = removed = [];
            foreach (var path in selected)
            {
                source.PhotoPaths.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
                source.ManualPhotoPaths.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
                if (!removed.Contains(path, StringComparer.OrdinalIgnoreCase)) removed.Add(path);
            }
            source.CoverPath = source.PhotoPaths.FirstOrDefault() ?? string.Empty;
        }, cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot.Undo is null) return false;
            snapshot.Albums = snapshot.Undo.Albums;
            snapshot.RemovedPhotos = snapshot.Undo.RemovedPhotos;
            snapshot.Undo = null;
            await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task MutateAsync(Action<PeopleAlbumSnapshot> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            var before = JsonSerializer.Serialize(new PeopleAlbumUndo { Albums = snapshot.Albums, RemovedPhotos = snapshot.RemovedPhotos }, JsonOptions);
            mutation(snapshot);
            var after = JsonSerializer.Serialize(new PeopleAlbumUndo { Albums = snapshot.Albums, RemovedPhotos = snapshot.RemovedPhotos }, JsonOptions);
            if (before == after) return;
            snapshot.Undo = JsonSerializer.Deserialize<PeopleAlbumUndo>(before, JsonOptions);
            await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<PeopleAlbumSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        var storePath = ResolveStorePath();
        if (!File.Exists(storePath)) return NewSnapshot();
        try
        {
            await using var stream = File.OpenRead(storePath);
            var snapshot = await JsonSerializer.DeserializeAsync<PeopleAlbumSnapshot>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? NewSnapshot();
            snapshot.Albums ??= [];
            snapshot.RemovedPhotos = snapshot.RemovedPhotos is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(snapshot.RemovedPhotos, StringComparer.OrdinalIgnoreCase);
            if (snapshot.Version <= 1 && _embeddingService.ModelIdentity == FaceModelIdentity.YuNetSFaceLegacy)
            {
                snapshot.Version = 2;
                snapshot.ModelIdentity = FaceModelIdentity.YuNetSFaceLegacy.StorageKey;
                snapshot.MatchThreshold = FaceModelIdentity.YuNetSFaceLegacy.MatchThreshold;
            }
            else if (!string.Equals(snapshot.ModelIdentity, _embeddingService.ModelIdentity.StorageKey, StringComparison.Ordinal))
            {
                throw new FaceModelMismatchException(snapshot.ModelIdentity, _embeddingService.ModelIdentity.StorageKey);
            }
            return snapshot;
        }
        catch (JsonException exception) { throw new InvalidDataException("人物记录损坏，已停止写入以保留原记录。", exception); }
    }

    private async Task SaveCoreAsync(PeopleAlbumSnapshot snapshot, CancellationToken cancellationToken)
    {
        var storePath = ResolveStorePath();
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = storePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, storePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private string ResolveStorePath()
    {
        if (!string.IsNullOrWhiteSpace(_storePath)) return _storePath;
        if (_embeddingService.ModelIdentity == FaceModelIdentity.YuNetSFaceLegacy)
            return Path.Combine(AppDataPaths.Root, "people-albums.json");
        var safeKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(_embeddingService.ModelIdentity.StorageKey))).ToLowerInvariant()[..16];
        return Path.Combine(AppDataPaths.Root, $"people-albums.{safeKey}.json");
    }

    private PeopleAlbumSnapshot NewSnapshot() => new()
    {
        Version = 2,
        ModelIdentity = _embeddingService.ModelIdentity.StorageKey,
        MatchThreshold = _embeddingService.ModelIdentity.MatchThreshold,
        RemovedPhotos = new(StringComparer.OrdinalIgnoreCase)
    };

    private static PersonAlbum? FindBestAlbum(IEnumerable<PersonAlbum> albums, IReadOnlyList<float> embedding, double matchThreshold) =>
        albums.Select(album => (Album: album, Score: album.MatchCentroids.Count == 0
                ? -1
                : album.MatchCentroids.Max(centroid => Cosine(centroid, embedding))))
            .Where(item => item.Score >= matchThreshold)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Album)
            .FirstOrDefault();

    private static double Cosine(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        if (first.Count == 0 || first.Count != second.Count) return -1;
        double dot = 0, a = 0, b = 0;
        for (var i = 0; i < first.Count; i++)
        {
            dot += first[i] * second[i];
            a += first[i] * first[i];
            b += second[i] * second[i];
        }
        return a <= 0 || b <= 0 ? -1 : dot / (Math.Sqrt(a) * Math.Sqrt(b));
    }

    private static bool IsRemoved(PeopleAlbumSnapshot snapshot, string albumId, string path) =>
        snapshot.RemovedPhotos.TryGetValue(albumId, out var removed)
        && removed.Contains(path, StringComparer.OrdinalIgnoreCase);

    private static string NextDefaultName(IEnumerable<PersonAlbum> albums)
    {
        var used = albums.Select(album => album.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < 26; index++)
        {
            var name = ((char)('A' + index)).ToString();
            if (!used.Contains(name)) return name;
        }
        return $"人物 {used.Count + 1}";
    }
    private static IReadOnlyList<PeopleScanAlbumProgress> CreateProgressAlbums(IEnumerable<PersonAlbum> albums) =>
        albums.OrderBy(album => album.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(album => new PeopleScanAlbumProgress(
                album.Id, album.Name, album.CoverPath, album.PhotoPaths.ToArray()))
            .ToArray();

    private string CreateFaceCover(DetectedFace face, string albumId)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(face.SourcePath);
            image.Mutate(context => context.AutoOrient());
            var side = Math.Max(face.Width, face.Height) * 1.5;
            var centerX = face.X + face.Width / 2d;
            var centerY = face.Y + face.Height / 2d;
            var left = Math.Clamp((int)Math.Round(centerX - side / 2), 0, Math.Max(0, image.Width - 1));
            var top = Math.Clamp((int)Math.Round(centerY - side / 2), 0, Math.Max(0, image.Height - 1));
            var width = Math.Min((int)Math.Round(side), image.Width - left);
            var height = Math.Min((int)Math.Round(side), image.Height - top);
            if (width <= 0 || height <= 0) return face.SourcePath;
            image.Mutate(context => context.Crop(new SixLabors.ImageSharp.Rectangle(left, top, width, height)));
            var directory = ResolveCoverDirectory();
            Directory.CreateDirectory(directory);
            var coverPath = Path.Combine(directory, $"{albumId}.jpg");
            image.SaveAsJpeg(coverPath);
            return coverPath;
        }
        catch
        {
            return face.SourcePath;
        }
    }

    private string ResolveCoverDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_storePath))
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_storePath))!, ".face-covers");
        var safeKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(_embeddingService.ModelIdentity.StorageKey))).ToLowerInvariant()[..16];
        return Path.Combine(AppDataPaths.Root, "face-covers", safeKey);
    }
}

/// <summary>人物照片扫描的总体进度。</summary>
public sealed record PeopleScanProgress(
    int Processed,
    int Total,
    int DetectedFaces,
    int People,
    IReadOnlyList<PeopleScanAlbumProgress> Albums);

/// <summary>单个人物相册生成阶段的进度。</summary>
public sealed record PeopleScanAlbumProgress(
    string Id,
    string Name,
    string CoverPath,
    IReadOnlyList<string> PhotoPaths)
{
    public int PhotoCount => PhotoPaths.Count;
}

/// <summary>一次人物扫描得到的相册集合和统计快照。</summary>
public sealed class PeopleAlbumSnapshot
{
    public int Version { get; set; } = 2;
    public string ModelIdentity { get; set; } = string.Empty;
    public double MatchThreshold { get; set; } = FaceRecognitionDefaults.YuNetSFaceThreshold;
    public List<PersonAlbum> Albums { get; set; } = [];
    public Dictionary<string, List<string>> RemovedPhotos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public PeopleAlbumUndo? Undo { get; set; }
}

public sealed class PeopleAlbumUndo
{
    public List<PersonAlbum> Albums { get; set; } = [];
    public Dictionary<string, List<string>> RemovedPhotos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>人物标签、封面和其包含媒体路径组成的相册。</summary>
public sealed class PersonAlbum
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public List<string> PhotoPaths { get; set; } = [];
    public List<float[]> MatchCentroids { get; set; } = [];
    public List<string> ManualPhotoPaths { get; set; } = [];
}
