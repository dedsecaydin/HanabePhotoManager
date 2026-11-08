using System.Text.Json;

namespace HanabePhotoManager.App.Services;

/// <summary>Migrates first-stage settings once without replacing existing shared preferences.</summary>
public static class MacSettingsMigration
{
    private sealed class LegacySettings
    {
        public string? LibraryRoot { get; set; }
        public string? Theme { get; set; } = "Dark";
        public bool MoveAfterVerify { get; set; }
        public bool SkipDuplicates { get; set; } = true;
    }

    public static async Task MigrateAsync(AppSettingsStore store, string legacyPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        if ((await store.LoadAsync(cancellationToken).ConfigureAwait(false)).MacSettingsMigrated) return;
        LegacySettings? legacy = null;
        if (File.Exists(legacyPath))
        {
            await using var stream = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            legacy = await JsonSerializer.DeserializeAsync<LegacySettings>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("Legacy settings are empty.");
        }
        await store.UpdateAsync(settings =>
        {
            if (settings.MacSettingsMigrated) return;
            if (legacy is not null)
            {
                if (string.IsNullOrWhiteSpace(settings.LibraryRoot)) settings.LibraryRoot = legacy.LibraryRoot;
                settings.MacTheme = legacy.Theme == "Light" ? "Light" : "Dark";
                settings.MacMoveAfterVerify = legacy.MoveAfterVerify;
                settings.MacSkipDuplicates = legacy.SkipDuplicates;
            }
            settings.MacSettingsMigrated = true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
