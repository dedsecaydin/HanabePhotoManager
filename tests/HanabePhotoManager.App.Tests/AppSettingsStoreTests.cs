using FluentAssertions;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task ImportSources_RoundTripWithoutChangingExistingSettingsShape()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hanabe-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AppSettingsStore(path);
            var settings = new AppSettings
            {
                LibraryRoot = "library",
                ImportSources =
                [
                    new ImportSourceSettings
                    {
                        Path = @"D:\Photos",
                        IsEnabled = false,
                        IncludeSubdirectories = true,
                        AutoWatch = true
                    }
                ]
            };

            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            loaded.LibraryRoot.Should().Be("library");
            loaded.ImportSources.Should().ContainSingle();
            loaded.ImportSources[0].Path.Should().Be(@"D:\Photos");
            loaded.ImportSources[0].IsEnabled.Should().BeFalse();
            loaded.ImportSources[0].AutoWatch.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
    [Fact]
    public async Task NavigationPreferencesSurviveRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-settings-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new AppSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                NavigationOrder = ["Preview", "Home"],
                NavigationDisplayMode = NavigationDisplayMode.Icon
            });

            var loaded = await new AppSettingsStore(path).LoadAsync();

            loaded.NavigationOrder.Should().Equal("Preview", "Home");
            loaded.NavigationDisplayMode.Should().Be(NavigationDisplayMode.Icon);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_PreservesFieldsOwnedByOtherSettingsSections()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-settings-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new AppSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                BaiduAppKey = "cloud-key",
                BaiduAppSecretProtected = "protected-secret",
                QuarkClientPath = "C:\\Quark\\Quark.exe"
            });

            await store.UpdateAsync(settings => settings.GlassIntensity = 0.75);

            var loaded = await store.LoadAsync();
            loaded.GlassIntensity.Should().Be(0.75);
            loaded.BaiduAppKey.Should().Be("cloud-key");
            loaded.BaiduAppSecretProtected.Should().Be("protected-secret");
            loaded.QuarkClientPath.Should().Be("C:\\Quark\\Quark.exe");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LegacyThumbnailSize_MigratesToDefaultThumbnailSize()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-settings-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "{\"ThumbnailSize\":212}");

            var loaded = await new AppSettingsStore(path).LoadAsync();

            loaded.DefaultThumbnailSize.Should().Be(212);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RecognitionAndBrowseDefaultsSurviveRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-settings-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var writer = new AppSettingsStore(path);
            await writer.SaveAsync(new AppSettings
            {
                ClassificationEngine = PhotoClassifierFactory.MobileClipMode,
                InferenceDevice = "CPU",
                SemanticMaxLabels = 5,
                SemanticSimilarityWindow = 0.06,
                DefaultRatingFilter = "4★",
                DefaultPreviewSort = 7
            });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.ClassificationEngine.Should().Be(PhotoClassifierFactory.MobileClipMode);
            loaded.InferenceDevice.Should().Be("CPU");
            loaded.SemanticMaxLabels.Should().Be(5);
            loaded.SemanticSimilarityWindow.Should().Be(0.06);
            loaded.DefaultRatingFilter.Should().Be("4★");
            loaded.DefaultPreviewSort.Should().Be(7);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
