using System.IO;
using System.Text.Json;
using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

public interface IMediaMetadataStore
{
    Task<MediaMetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task<MediaMetadataEntry?> GetAsync(string path, CancellationToken cancellationToken = default);

    Task UpsertAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default);

    Task SaveAsync(MediaMetadataSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class MediaMetadataStore : IMediaMetadataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MediaMetadataStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppDataPaths.Root, "media-metadata.json");
    }

    public async Task<MediaMetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<MediaMetadataEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(MediaMetadataEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            entry.Path = NormalizePath(entry.Path);
            var existing = snapshot.Entries.FindIndex(candidate =>
                string.Equals(candidate.Path, entry.Path, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) snapshot.Entries[existing] = entry;
            else snapshot.Entries.Add(entry);
            await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(MediaMetadataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await SaveCoreAsync(snapshot, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<MediaMetadataSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new MediaMetadataSnapshot();
        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<MediaMetadataSnapshot>(stream, JsonOptions, cancellationToken)
                       .ConfigureAwait(false) ?? new MediaMetadataSnapshot();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            PreserveCorruptDocument();
            return new MediaMetadataSnapshot();
        }
    }

    private async Task SaveCoreAsync(MediaMetadataSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        snapshot.Version = 1;
        foreach (var entry in snapshot.Entries) entry.Path = NormalizePath(entry.Path);

        var temporary = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                             64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void PreserveCorruptDocument()
    {
        if (!File.Exists(_path)) return;
        var directory = Path.GetDirectoryName(_path) ?? string.Empty;
        var backup = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(_path)}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(_path)}");
        try { File.Move(_path, backup); }
        catch (IOException) { }
    }

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
}
