using System.IO;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacSettingsMigrationTests
{
    [Fact]
    public async Task MigrationPreservesSharedValuesAndDoesNotRepeat()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "settings-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var shared = new AppSettingsStore(Path.Combine(root, "shared.json"));
            var existingRoot = Path.Combine(root, "existing-library");
            await shared.SaveAsync(new AppSettings { LibraryRoot = existingRoot, DefaultCompressionMegabytes = 9 });
            var legacy = Path.Combine(root, "legacy.json");
            await File.WriteAllTextAsync(legacy, "{\"LibraryRoot\":\"ignored\",\"Theme\":\"Light\",\"MoveAfterVerify\":true,\"SkipDuplicates\":false}");
            await MacSettingsMigration.MigrateAsync(shared, legacy);
            var result = await shared.LoadAsync();
            Assert.Equal(existingRoot, result.LibraryRoot);
            Assert.Equal(9, result.DefaultCompressionMegabytes);
            Assert.Equal("Light", result.MacTheme);
            Assert.True(result.MacMoveAfterVerify);
            Assert.False(result.MacSkipDuplicates);
            await File.WriteAllTextAsync(legacy, "{\"Theme\":\"Dark\"}");
            await MacSettingsMigration.MigrateAsync(shared, legacy);
            Assert.Equal("Light", (await shared.LoadAsync()).MacTheme);
            Assert.True(File.Exists(legacy));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidLegacyDataDoesNotModifySharedSettings()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "settings-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var shared = new AppSettingsStore(Path.Combine(root, "shared.json"));
            await shared.SaveAsync(new AppSettings { DefaultCompressionMegabytes = 7 });
            var legacy = Path.Combine(root, "legacy.json");
            await File.WriteAllTextAsync(legacy, "{");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => MacSettingsMigration.MigrateAsync(shared, legacy));
            Assert.Equal(7, (await shared.LoadAsync()).DefaultCompressionMegabytes);
            Assert.Equal("{", await File.ReadAllTextAsync(legacy));
        }
        finally { Directory.Delete(root, true); }
    }
}
