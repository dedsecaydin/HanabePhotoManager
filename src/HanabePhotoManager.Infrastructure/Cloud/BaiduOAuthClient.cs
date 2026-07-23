using System.Net.Http.Headers;
using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>Small client for Baidu's documented OAuth 2.0 authorization-code flow.</summary>
public sealed class BaiduOAuthClient
{
    public static readonly Uri AuthorizeEndpoint = new("https://openapi.baidu.com/oauth/2.0/authorize");
    public static readonly Uri TokenEndpoint = new("https://openapi.baidu.com/oauth/2.0/token");

    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _pendingStates = new(StringComparer.Ordinal);
    private readonly object _stateGate = new();

    public BaiduOAuthClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Uri BuildAuthorizeUri(string clientId, string state)
    {
        ValidateRequired(clientId, nameof(clientId));
        ValidateRequired(state, nameof(state));
        var query = new List<string>
        {
            "response_type=code",
            "client_id=" + Uri.EscapeDataString(clientId),
            "redirect_uri=oob",
            "scope=" + Uri.EscapeDataString("basic,netdisk")
        };
        query.Add("state=" + Uri.EscapeDataString(state));
        lock (_stateGate) _pendingStates.Add(state);
        return new Uri(AuthorizeEndpoint + "?" + string.Join('&', query), UriKind.Absolute);
    }

    public Task<CloudAuthToken> ExchangeCodeAsync(
        string authorizationCode,
        string clientId,
        string clientSecret,
        string expectedState,
        CancellationToken cancellationToken = default) =>
        RequestTokenWithStateAsync(
            expectedState,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = Required(authorizationCode, nameof(authorizationCode)),
                ["client_id"] = Required(clientId, nameof(clientId)),
                ["client_secret"] = Required(clientSecret, nameof(clientSecret)),
                ["redirect_uri"] = "oob"
            },
            cancellationToken,
            requireRefreshToken: true);

    private Task<CloudAuthToken> RequestTokenWithStateAsync(string expectedState, Dictionary<string, string> form, CancellationToken cancellationToken, bool requireRefreshToken)
    {
        ValidateRequired(expectedState, nameof(expectedState));
        lock (_stateGate)
        {
            if (!_pendingStates.Remove(expectedState))
                throw new ArgumentException("OAuth state does not match a pending authorization request.", nameof(expectedState));
        }
        return RequestTokenAsync(form, cancellationToken, requireRefreshToken);
    }

    public async Task<CloudAuthToken> RefreshAndPersistAsync(CloudAuthToken currentToken, string clientId, string clientSecret, ICloudSessionStore sessionStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        var refreshed = await RefreshAsync(currentToken, clientId, clientSecret, cancellationToken).ConfigureAwait(false);
        // Refresh-token rotation invalidates the old token at the provider. Once a new token is received,
        // finish the short atomic local write even if the caller cancels, so a restart cannot lose the session.
        await sessionStore.SaveAsync(refreshed, CancellationToken.None).ConfigureAwait(false);
        return refreshed;
    }

    public Task<CloudAuthToken> RefreshAsync(
        CloudAuthToken currentToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentToken);
        if (currentToken.Provider != CloudProviderKind.Baidu)
            throw new ArgumentException("The token must belong to Baidu.", nameof(currentToken));
        return RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentToken.RefreshToken,
                ["client_id"] = Required(clientId, nameof(clientId)),
                ["client_secret"] = Required(clientSecret, nameof(clientSecret))
            },
            cancellationToken,
            requireRefreshToken: true);
    }

    private async Task<CloudAuthToken> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken,
        bool requireRefreshToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateRemoteException(response, payload);

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                var code = error.GetString() ?? "oauth_error";
                throw new HttpRequestException($"Baidu OAuth failed: {code}.");
            }

            var access = root.GetProperty("access_token").GetString();
            var refresh = root.TryGetProperty("refresh_token", out var refreshElement)
                ? refreshElement.GetString()
                : null;
            var expiresIn = root.GetProperty("expires_in").GetInt64();
            if (string.IsNullOrWhiteSpace(access) || (requireRefreshToken && string.IsNullOrWhiteSpace(refresh)) || expiresIn <= 0)
                throw new InvalidDataException("Baidu OAuth response did not contain a complete token set.");

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["token_endpoint"] = TokenEndpoint.ToString()
            };
            return new CloudAuthToken(
                CloudProviderKind.Baidu,
                access!,
                refresh!,
                DateTimeOffset.UtcNow.AddSeconds(expiresIn),
                metadata);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Baidu OAuth returned malformed JSON.", ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException("Baidu OAuth response did not contain a valid token.", ex);
        }
    }

    private static HttpRequestException CreateRemoteException(HttpResponseMessage response, string payload)
    {
        var code = "HTTP_" + (int)response.StatusCode;
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                code = error.GetString() ?? code;
        }
        catch (JsonException) { }
        return new HttpRequestException($"Baidu OAuth failed: {code}.", null, response.StatusCode);
    }

    private static string Required(string value, string name) { ValidateRequired(value, name); return value; }
    private static void ValidateRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name);
    }
}
