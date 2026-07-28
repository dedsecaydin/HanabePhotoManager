using FluentAssertions;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AppSettingsStoreTests
{
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
    public async Task OnboardingCompletionSurvivesRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-onboarding-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            await new AppSettingsStore(path).SaveAsync(new AppSettings { HasCompletedOnboarding = true });

            var loaded = await new AppSettingsStore(path).LoadAsync();

            loaded.HasCompletedOnboarding.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
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
                FaceRecognitionEngine = FaceRecognitionEngineKind.ArcFaceR100,
                FaceRecognitionProfile = FaceRecognitionProfile.HighAccuracy,
                ArcFaceDetectorModelPath = "D:\\models\\detector.onnx",
                ArcFaceRecognizerModelPath = "D:\\models\\r100.onnx",
                ArcFaceModelLicenseConfirmed = true,
                ArcFaceModelLicenseDescription = "Self-trained model",
                ArcFaceMatchThreshold = 0.48,
                SemanticMaxLabels = 5,
                SemanticSimilarityWindow = 0.06,
                DefaultRatingFilter = "4★",
                DefaultPreviewSort = 7
            });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.ClassificationEngine.Should().Be(PhotoClassifierFactory.MobileClipMode);
            loaded.InferenceDevice.Should().Be("CPU");
            loaded.FaceRecognitionEngine.Should().Be(FaceRecognitionEngineKind.ArcFaceR100);
            loaded.FaceRecognitionProfile.Should().Be(FaceRecognitionProfile.HighAccuracy);
            loaded.ArcFaceDetectorModelPath.Should().EndWith("detector.onnx");
            loaded.ArcFaceRecognizerModelPath.Should().EndWith("r100.onnx");
            loaded.ArcFaceModelLicenseConfirmed.Should().BeTrue();
            loaded.ArcFaceModelLicenseDescription.Should().Be("Self-trained model");
            loaded.ArcFaceMatchThreshold.Should().Be(0.48);
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
