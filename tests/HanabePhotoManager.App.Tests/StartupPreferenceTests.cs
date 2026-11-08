using System.IO;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class StartupPreferenceTests
{
    [Fact]
    public async Task SavesActualRegistrationWithoutChangingOtherPreferences()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "startup-preference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            await store.SaveAsync(new AppSettings { DefaultCompressionMegabytes = 7 });
            var platform = new FakeStartup();
            var preference = new StartupPreferenceService(platform, store);
            await preference.SetEnabledAsync(true);
            Assert.True(platform.Enabled);
            var saved = await store.LoadAsync();
            Assert.True(saved.LaunchAtStartup);
            Assert.Equal(7, saved.DefaultCompressionMegabytes);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FailedSaveRestoresPreviousRegistration()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "startup-preference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            var platform = new FakeStartup();
            var preference = new StartupPreferenceService(platform, store);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preference.SetEnabledAsync(true, cancellation.Token));
            Assert.False(platform.Enabled);
            Assert.False((await store.LoadAsync()).LaunchAtStartup);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RegistrationFailureDoesNotChangeStoredPreference()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "startup-preference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new AppSettingsStore(Path.Combine(root, "settings.json"));
            var preference = new StartupPreferenceService(new FakeStartup { Fail = true }, store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => preference.SetEnabledAsync(true));
            Assert.False((await store.LoadAsync()).LaunchAtStartup);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FakeStartup : IStartupRegistrationService
    {
        public bool Enabled { get; private set; }
        public bool Fail { get; init; }
        public bool IsEnabled() => Enabled;
        public void SetEnabled(bool enabled)
        {
            if (Fail) throw new InvalidOperationException("registration failed");
            Enabled = enabled;
        }
    }
}
