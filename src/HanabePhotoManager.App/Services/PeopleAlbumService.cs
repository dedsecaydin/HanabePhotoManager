using System.IO;
using System.Text.Json;

namespace HanabePhotoManager.App.Services;

public sealed class PeopleAlbumService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const double MatchThreshold = 0.62;
    private readonly string _storePath;
    private readonly ILocalFaceEmbeddingService _embeddingService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PeopleAlbumService(string? storePath = null, ILocalFaceEmbeddingService? embeddingService = null)
    {
        _storePath = storePath ?? Path.Combine(AppDataPaths.Root, "people-albums.json");
        _embeddingService = embeddingService ?? new LocalFaceEmbeddingService();
    }

    public async Task<PeopleAlbumSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<PeopleAlbumSnapshot> ScanAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
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
                album.PhotoPaths.RemoveAll(path => rescanned.Contains(Path.GetFullPath(path)));

            foreach (var path in scanPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<DetectedFace> faces;
                try { faces = await _embeddingService.DetectAsync(path, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { continue; }

                foreach (var face in faces)
                {
                    var album = FindBestAlbum(snapshot.Albums, face.Embedding);
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
                    if (!IsRemoved(snapshot, album.Id, path)
                        && !album.PhotoPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                        album.PhotoPaths.Add(path);
                    if (string.IsNullOrWhiteSpace(album.CoverPath) || !File.Exists(album.CoverPath))
                        album.CoverPath = path;
                }
            }

            snapshot.Albums.RemoveAll(album => album.PhotoPaths.Count == 0 && string.IsNullOrWhiteSpace(album.Name));
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
            if (!snapshot.RemovedPhotos.TryGetValue(albumId, out var removed))
                snapshot.RemovedPhotos[albumId] = removed = [];
            if (!removed.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) removed.Add(fullPath);
            snapshot.Albums.FirstOrDefault(item => item.Id == albumId)?.PhotoPaths
                .RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
        }, cancellationToken);

    public Task MergeAsync(string targetId, string sourceId, CancellationToken cancellationToken) =>
        MutateAsync(snapshot =>
        {
            var target = snapshot.Albums.FirstOrDefault(item => item.Id == targetId);
            var source = snapshot.Albums.FirstOrDefault(item => item.Id == sourceId);
            if (target is null || source is null || ReferenceEquals(target, source)) return;
            foreach (var centroid in source.MatchCentroids) target.MatchCentroids.Add(centroid);
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

    private async Task MutateAsync(Action<PeopleAlbumSnapshot> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            mutation(snapshot);
            await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<PeopleAlbumSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath)) return NewSnapshot();
        try
        {
            await using var stream = File.OpenRead(_storePath);
            var snapshot = await JsonSerializer.DeserializeAsync<PeopleAlbumSnapshot>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? NewSnapshot();
            snapshot.Albums ??= [];
            snapshot.RemovedPhotos = snapshot.RemovedPhotos is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(snapshot.RemovedPhotos, StringComparer.OrdinalIgnoreCase);
            return snapshot;
        }
        catch (JsonException) { return NewSnapshot(); }
    }

    private async Task SaveCoreAsync(PeopleAlbumSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = _storePath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _storePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static PeopleAlbumSnapshot NewSnapshot() => new()
    {
        RemovedPhotos = new(StringComparer.OrdinalIgnoreCase)
    };

    private static PersonAlbum? FindBestAlbum(IEnumerable<PersonAlbum> albums, IReadOnlyList<float> embedding) =>
        albums.Select(album => (Album: album, Score: album.MatchCentroids.Count == 0
                ? -1
                : album.MatchCentroids.Max(centroid => Cosine(centroid, embedding))))
            .Where(item => item.Score >= MatchThreshold)
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
}

public sealed class PeopleAlbumSnapshot
{
    public int Version { get; set; } = 1;
    public List<PersonAlbum> Albums { get; set; } = [];
    public Dictionary<string, List<string>> RemovedPhotos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PersonAlbum
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public List<string> PhotoPaths { get; set; } = [];
    public List<float[]> MatchCentroids { get; set; } = [];
}
