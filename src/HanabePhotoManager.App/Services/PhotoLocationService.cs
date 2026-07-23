using System.IO;
using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

public sealed class PhotoLocationService
{
    private readonly IMediaMetadataStore _store;

    public PhotoLocationService(IMediaMetadataStore store) => _store = store;

    public async Task<PhotoLocation?> GetEffectiveAsync(string path, CancellationToken cancellationToken) =>
        (await _store.GetAsync(path, cancellationToken).ConfigureAwait(false))?.EffectiveLocation;

    public async Task<IReadOnlyList<LocatedPhoto>> GetLocatedAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken)
    {
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var requested = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return snapshot.Entries
            .Where(entry => requested.Contains(Path.GetFullPath(entry.Path)) && entry.EffectiveLocation is not null)
            .Select(entry => new LocatedPhoto(Path.GetFullPath(entry.Path), entry.EffectiveLocation!))
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task AssignManualAsync(
        IEnumerable<string> paths,
        double latitude,
        double longitude,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var coordinate = ExifLocationReader.Validate(latitude, longitude)
            ?? throw new ArgumentOutOfRangeException(nameof(latitude), "位置坐标超出有效范围。");
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        snapshot.Entries ??= [];
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var entry = snapshot.Entries.FirstOrDefault(candidate =>
                string.Equals(Path.GetFullPath(candidate.Path), path, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                entry = new MediaMetadataEntry { Path = path };
                snapshot.Entries.Add(entry);
            }
            entry.ManualLocation = new PhotoLocation(
                coordinate.Latitude, coordinate.Longitude, PhotoLocationSource.Manual,
                string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim());
        }
        await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearManualAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var requested = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in snapshot.Entries.Where(entry => requested.Contains(Path.GetFullPath(entry.Path))))
            entry.ManualLocation = null;
        await _store.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<LocationCluster> Cluster(IEnumerable<LocatedPhoto> photos, int zoom)
    {
        var clampedZoom = Math.Clamp(zoom, 1, 20);
        var cellSize = 360d / Math.Pow(2, clampedZoom + 4);
        return photos.GroupBy(photo =>
            $"{Math.Floor((photo.Location.Latitude + 90) / cellSize):00000000}:" +
            $"{Math.Floor((photo.Location.Longitude + 180) / cellSize):00000000}")
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new LocationCluster(
                group.Key,
                group.Average(item => item.Location.Latitude),
                group.Average(item => item.Location.Longitude),
                group.Count(),
                group.Select(item => item.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }
}

public sealed record LocatedPhoto(string Path, PhotoLocation Location);

public sealed record LocationCluster(
    string Key,
    double Latitude,
    double Longitude,
    int Count,
    IReadOnlyList<string> PhotoPaths);
