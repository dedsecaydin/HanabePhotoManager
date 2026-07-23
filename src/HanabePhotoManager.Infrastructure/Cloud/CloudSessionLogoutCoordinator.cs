using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>Clears local session first, then asks the provider scheduler to pause work.</summary>
public sealed class CloudSessionLogoutCoordinator
{
    private readonly ICloudSessionStore _sessionStore;
    private readonly Func<CloudProviderKind, CancellationToken, Task> _pauseProviderTasks;

    public CloudSessionLogoutCoordinator(ICloudSessionStore sessionStore, Func<CloudProviderKind, CancellationToken, Task> pauseProviderTasks)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _pauseProviderTasks = pauseProviderTasks ?? throw new ArgumentNullException(nameof(pauseProviderTasks));
    }

    public async Task LogoutAsync(CloudProviderKind provider, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        await _sessionStore.DeleteAsync(provider, cancellationToken).ConfigureAwait(false);
        await _pauseProviderTasks(provider, cancellationToken).ConfigureAwait(false);
    }
}
