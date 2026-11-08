namespace HanabePhotoManager.App.Services;

/// <summary>Coordinates system registration and the persisted startup preference.</summary>
public sealed class StartupPreferenceService(IStartupRegistrationService registration, AppSettingsStore settings)
{
    public bool IsEnabled() => registration.IsEnabled();

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var previous = registration.IsEnabled();
        registration.SetEnabled(enabled);
        try
        {
            await settings.UpdateAsync(current => current.LaunchAtStartup = enabled, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception saveError)
        {
            try { registration.SetEnabled(previous); }
            catch (Exception rollbackError) { throw new AggregateException("Startup preference could not be saved or restored.", saveError, rollbackError); }
            throw;
        }
    }
}
