using System.IO;
using System.Text.Json;
using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

public interface IPhotoAnalysisCheckpointStore
{
    Task<IReadOnlyList<MediaMetadataEntry>> LoadAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class PhotoAnalysisCheckpointStore : IPhotoAnalysisCheckpointStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public PhotoAnalysisCheckpointStore(string? path = null) =>
        _path = path ?? Path.Combine(AppDataPaths.Root, "photo-analysis.checkpoint.jsonl");

    public async Task<IReadOnlyList<MediaMetadataEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        var result = new Dictionary<string, MediaMetadataEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in await File.ReadAllLinesAsync(_path, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<MediaMetadataEntry>(line);
                if (entry is not null && !string.IsNullOrWhiteSpace(entry.Path)) result[Path.GetFullPath(entry.Path)] = entry;
            }
            catch (JsonException) { }
        }
        return result.Values.ToArray();
    }

    public async Task AppendAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.AppendAllTextAsync(_path, JsonSerializer.Serialize(entry) + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { if (File.Exists(_path)) File.Delete(_path); }
        finally { _gate.Release(); }
    }
}
