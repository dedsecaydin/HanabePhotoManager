using HanabePhotoManager.App.Compression;

namespace HanabePhotoManager.App.Services;

public sealed record MapMediaScanResult(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

public sealed class MapMediaSourceService
{
    private readonly ImageInputDiscovery _discovery = new();

    public Task<MapMediaScanResult> ScanAsync(
        IEnumerable<string> roots,
        bool recursive,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = _discovery.Discover(roots, recursive, cancellationToken);
            return new MapMediaScanResult(result.Files, result.Warnings);
        }, cancellationToken);
    }
}
