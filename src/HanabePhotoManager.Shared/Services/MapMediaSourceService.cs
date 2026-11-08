using HanabePhotoManager.App.Compression;

namespace HanabePhotoManager.App.Services;

/// <summary>地图媒体扫描得到的候选文件与无法读取目录的警告。</summary>
public sealed record MapMediaScanResult(IReadOnlyList<string> Files, IReadOnlyList<string> Warnings);

/// <summary>从照片库增量发现可读取地理元数据的媒体文件。</summary>
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
