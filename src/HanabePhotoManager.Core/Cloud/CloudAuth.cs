namespace HanabePhotoManager.Core.Cloud;

/// <summary>Refreshable cloud session material. Passwords, one-time codes and QR content are never represented.</summary>
public sealed record CloudAuthToken
{
    public CloudAuthToken(
        CloudProviderKind provider,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        IReadOnlyDictionary<string, string>? appMetadata = null)
    {
        if (!Enum.IsDefined(provider))
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "Cloud provider is undefined.");
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token is required.", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

        Provider = provider;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
        AppMetadata = new Dictionary<string, string>(appMetadata ??
            new Dictionary<string, string>(), StringComparer.Ordinal);
    }

    public CloudProviderKind Provider { get; }
    public string AccessToken { get; }
    public string RefreshToken { get; }
    public DateTimeOffset ExpiresAt { get; }
    public IReadOnlyDictionary<string, string> AppMetadata { get; }
}

public interface ICloudSessionStore
{
    Task SaveAsync(CloudAuthToken token, CancellationToken cancellationToken = default);
    Task<CloudAuthToken?> LoadAsync(CloudProviderKind provider, CancellationToken cancellationToken = default);
    Task DeleteAsync(CloudProviderKind provider, CancellationToken cancellationToken = default);
}
