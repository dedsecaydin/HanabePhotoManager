using System.IO;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacSettingsIsolationTests
{
    [Fact]
    public async Task SharedSettingsDoNotOverwriteExistingMacSettings()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var legacy = Path.Combine(root, "settings.json");
            await File.WriteAllTextAsync(legacy, "{\"Theme\":\"Dark\",\"SkipDuplicates\":false}");
            var shared = Path.Combine(MacSettingsStore.SharedDataDirectory(root), "settings.json");
            await new AppSettingsStore(shared).SaveAsync(new AppSettings());
            Assert.Contains("\"SkipDuplicates\":false", await File.ReadAllTextAsync(legacy));
            Assert.True(File.Exists(shared));
        }
        finally { Directory.Delete(root, true); }
    }
}
