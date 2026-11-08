using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HanabePhotoManager.App.Services;

/// <summary>Fetches one requested map tile and caches it according to HTTP headers.</summary>
public sealed class MapTileCache
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private readonly string _root, _template;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const int MaximumBytes = 2 * 1024 * 1024;

    public MapTileCache(string root, HttpClient client, string template = "https://tile.openstreetmap.org/{z}/{x}/{y}.png")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (!template.Contains("{z}") || !template.Contains("{x}") || !template.Contains("{y}") || !template.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("瓦片地址必须是包含 z/x/y 的 HTTPS 模板。", nameof(template));
        _template = template;
        var provider = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(template)))[..16];
        _root = Path.Combine(Path.GetFullPath(root), provider);
    }

    public async Task<byte[]> GetTileAsync(int zoom, int x, int y, CancellationToken token)
    {
        if (zoom is < 0 or > 22 || x < 0 || y < 0 || x >= (1 << zoom) || y >= (1 << zoom)) throw new ArgumentOutOfRangeException(nameof(zoom));
        var directory = Path.Combine(_root, zoom.ToString(), x.ToString());
        var file = Path.Combine(directory, y + ".png");
        var metadataPath = file + ".json";
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var metadata = await ReadMetadataAsync(metadataPath, token).ConfigureAwait(false);
            var cached = File.Exists(file) ? await File.ReadAllBytesAsync(file, token).ConfigureAwait(false) : null;
            if (cached is not null && !IsPng(cached)) cached = null;
            if (cached is not null && metadata?.ExpiresUtc > DateTimeOffset.UtcNow) return cached;

            var url = _template.Replace("{z}", zoom.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString());
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("HanabePhotoManager/0.4");
            if (cached is not null && EntityTagHeaderValue.TryParse(metadata?.ETag, out var etag)) request.Headers.IfNoneMatch.Add(etag);
            if (cached is not null && metadata?.LastModified is { } modified) request.Headers.IfModifiedSince = modified;
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            byte[] data;
            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null) data = cached;
            else
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidDataException("地图瓦片过大。");
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + count > MaximumBytes) throw new InvalidDataException("地图瓦片过大。");
                    buffer.Write(chunk, 0, count);
                }
                data = buffer.ToArray();
                if (!IsPng(data)) throw new InvalidDataException("地图瓦片不是 PNG 图像。");
            }
            var cacheControl = response.Headers.CacheControl;
            if (cacheControl?.NoStore != true)
            {
                var now = DateTimeOffset.UtcNow;
                var apparentAge = response.Headers.Date is { } date && now > date ? now - date : TimeSpan.Zero;
                var age = response.Headers.Age is { } headerAge && headerAge > apparentAge ? headerAge : apparentAge;
                var expires = cacheControl?.NoCache == true ? now : cacheControl?.MaxAge is { } maxAge ? now + (maxAge > age ? maxAge - age : TimeSpan.Zero) :
                    response.Content.Headers.Expires ?? now.AddDays(7);
                var entry = new TileMetadata(expires, response.Headers.ETag?.ToString() ?? metadata?.ETag,
                    response.Content.Headers.LastModified ?? metadata?.LastModified);
                Directory.CreateDirectory(directory);
                var suffix = "." + Guid.NewGuid().ToString("N") + ".tmp";
                var tempFile = file + suffix; var tempMetadata = metadataPath + suffix;
                try
                {
                    await File.WriteAllBytesAsync(tempFile, data, token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(tempMetadata, JsonSerializer.Serialize(entry), token).ConfigureAwait(false);
                    File.Move(tempFile, file, true); File.Move(tempMetadata, metadataPath, true);
                }
                finally { if (File.Exists(tempFile)) File.Delete(tempFile); if (File.Exists(tempMetadata)) File.Delete(tempMetadata); }
            }
            return data;
        }
        finally { _gate.Release(); }
    }

    private static bool IsPng(byte[] data) => data.Length >= PngSignature.Length && data.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
    private static async Task<TileMetadata?> ReadMetadataAsync(string path, CancellationToken token)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<TileMetadata>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false)) : null; }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }
    private sealed record TileMetadata(DateTimeOffset ExpiresUtc, string? ETag, DateTimeOffset? LastModified);
}
