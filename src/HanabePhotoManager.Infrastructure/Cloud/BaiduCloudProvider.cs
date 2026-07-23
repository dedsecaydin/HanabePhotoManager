using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>
/// Real Baidu Netdisk (百度网盘) cloud provider using the PCS REST API.
/// Requires an active OAuth token obtained through BaiduOAuthClient.
/// </summary>
public sealed class BaiduCloudProvider : ICloudProvider
{
    private const string BaseUrl = "https://pan.baidu.com/rest/2.0/xpan";

    private readonly HttpClient _httpClient;
    private readonly Func<Task<CloudAuthToken>> _tokenSource;

    public BaiduCloudProvider(HttpClient httpClient, Func<Task<CloudAuthToken>> tokenSource)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenSource = tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
    }

    public CloudProviderKind Kind => CloudProviderKind.Baidu;

    public async Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken)
    {
        var token = await _tokenSource().WaitAsync(cancellationToken).ConfigureAwait(false);
        using var request = CreateApiRequest("/nas", "uinfo", token.AccessToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);

        using var json = JsonDocument.Parse(body);
        // Baidu uinfo returns either flat fields or a nested "user_info" object.
        var root = json.RootElement;
        JsonElement lookup = root.TryGetProperty("user_info", out var userInfo) ? userInfo : root;

        long total = 0;
        long used = 0;
        if (lookup.TryGetProperty("total", out var totalProp)) total = totalProp.GetInt64();
        else if (lookup.TryGetProperty("total_size", out var totalSizeProp)) total = totalSizeProp.GetInt64();
        if (lookup.TryGetProperty("used", out var usedProp)) used = usedProp.GetInt64();
        else if (lookup.TryGetProperty("used_size", out var usedSizeProp)) used = usedSizeProp.GetInt64();

        return new CloudAccountState(
            Kind,
            true,
            "百度网盘",
            used,
            total,
            "已连接");
    }

    public async IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var token = await _tokenSource().WaitAsync(cancellationToken).ConfigureAwait(false);
        var dir = directory.Value == "/" ? "/" : directory.Value.TrimEnd('/');

        var start = 0;
        const int limit = 1000;
        var hasMore = true;

        while (hasMore)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queryString = $"&dir={Uri.EscapeDataString(dir)}&start={start}&limit={limit}&order=name&desc=0";
            using var request = CreateApiRequest("/file", "list", token.AccessToken, queryString);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, body);

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("list", out var listElement) || listElement.ValueKind != JsonValueKind.Array)
            {
                hasMore = false;
                yield break;
            }

            var count = 0;
            foreach (var entry in listElement.EnumerateArray())
            {
                count++;
                var path = entry.GetProperty("path").GetString() ?? "";
                var serverFilename = entry.GetProperty("server_filename").GetString() ?? "";
                var isDir = entry.GetProperty("isdir").GetInt32() != 0;
                var size = entry.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                var mtime = entry.TryGetProperty("server_mtime", out var mt) ? mt.GetInt64() : 0L;

                yield return new CloudObject(
                    Kind,
                    path, // remoteId
                    new CloudPath(path),
                    string.IsNullOrWhiteSpace(serverFilename)
                        ? Path.GetFileName(path.TrimEnd('/'))
                        : serverFilename,
                    isDir ? CloudObjectKind.Folder : GetObjectKind(serverFilename),
                    size,
                    mtime == 0 ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(mtime),
                    null, // thumbnailKey
                    false); // isHanabeManaged
            }

            hasMore = count >= limit;
            start += count;
        }
    }

    public Task<Stream?> OpenThumbnailAsync(CloudObject item, CancellationToken cancellationToken)
    {
        // Baidu Netdisk thumbnail API requires a separate endpoint; skip for now.
        return Task.FromResult<Stream?>(null);
    }

    public async Task<Stream> OpenReadAsync(CloudObject item, CancellationToken cancellationToken)
    {
        var token = await _tokenSource().WaitAsync(cancellationToken).ConfigureAwait(false);
        var remotePath = item.RemoteId;
        var queryString = $"&path={Uri.EscapeDataString(remotePath)}";
        using var request = CreateApiRequest("/file", "metas", token.AccessToken, queryString);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);

        using var json = JsonDocument.Parse(body);
        var dlink = json.RootElement.GetProperty("list")[0].GetProperty("dlink").GetString() ?? "";

        // Follow the dlink redirect to get the actual file content.
        using var dlRequest = new HttpRequestMessage(HttpMethod.Get, dlink);
        dlRequest.Headers.UserAgent.ParseAdd("pan.baidu.com");
        var dlResponse = await _httpClient.SendAsync(dlRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        return await dlResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<CloudObject> EnsureFolderAsync(CloudPath path, CancellationToken cancellationToken)
    {
        // Baidu Netdisk: create directory via /xpan/file?method=create
        throw new NotImplementedException("Baidu folder creation not yet implemented.");
    }

    public Task<string> UploadAsync(string localPath, CloudPath destination, IProgress<CloudUploadProgress>? progress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Baidu upload not yet implemented.");
    }

    public Task<CloudVerificationResult> VerifyAsync(string remoteId, CloudTransferFile expected, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Baidu verification not yet implemented.");
    }

    private static HttpRequestMessage CreateApiRequest(string resource, string method, string accessToken, string? extraQuery = null)
    {
        var url = $"{BaseUrl}{resource}?method={method}&access_token={Uri.EscapeDataString(accessToken)}" + (extraQuery ?? "");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("HanabePhotoManager/1.0");
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode) return;

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("errno", out var errno) && errno.GetInt32() != 0)
            {
                var errmsg = json.RootElement.TryGetProperty("errmsg", out var msg)
                    ? msg.GetString() ?? ""
                    : "";
                throw new HttpRequestException($"Baidu API error {errno.GetInt32()}: {errmsg}", null, response.StatusCode);
            }
        }
        catch (JsonException) { }

        throw new HttpRequestException($"Baidu API HTTP {(int)response.StatusCode}", null, response.StatusCode);
    }

    private static CloudObjectKind GetObjectKind(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp") return CloudObjectKind.Image;
        if (extension is ".arw" or ".cr2" or ".cr3" or ".nef" or ".raf" or ".rw2" or ".orf" or ".dng" or ".raw") return CloudObjectKind.Raw;
        if (extension is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".wmv") return CloudObjectKind.Video;
        if (extension is ".aac" or ".wav" or ".mp3" or ".m4a" or ".flac" or ".ogg") return CloudObjectKind.Audio;
        return CloudObjectKind.Other;
    }
}
