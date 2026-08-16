using FluentAssertions;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task GalleryGroupTitleMode_DefaultsToParsedDateAndPersists()
    {
        new AppSettings().GalleryGroupTitleMode.Should().Be(GalleryGroupTitleMode.ParsedDate);
        var path = Path.Combine(Path.GetTempPath(), $"hanabe-gallery-title-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AppSettingsStore(path);
            await store.SaveAsync(new AppSettings { GalleryGroupTitleMode = GalleryGroupTitleMode.ParsedDateAndFolderName });
            (await store.LoadAsync()).GalleryGroupTitleMode.Should().Be(GalleryGroupTitleMode.ParsedDateAndFolderName);
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
                LaunchAtStartup = true,
                DefaultThumbnailSize = 300,
                DefaultPreviewSort = 7
            });

            await store.UpdateAsync(settings => settings.GlassIntensity = 0.75);

            var loaded = await store.LoadAsync();
            loaded.GlassIntensity.Should().Be(0.75);
            loaded.LaunchAtStartup.Should().BeTrue();
            loaded.DefaultThumbnailSize.Should().Be(300);
            loaded.DefaultPreviewSort.Should().Be(7);
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
    public async Task LoadAsync_CorruptJson_FallsBackToDefaultsWithoutThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-corrupt-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            // 与真实事故同款损坏：未转义反斜杠（\h 非法转义）——JsonReaderException 触发点
            await File.WriteAllTextAsync(path, "{\"LibraryRoot\": \"D:\\hanabephoto\\.artifacts\\home-fix-fixture\"}");

            var loaded = await new AppSettingsStore(path).LoadAsync();

            loaded.Should().NotBeNull();
            loaded.LibraryRoot.Should().BeNull();
            loaded.HasCompletedOnboarding.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptJson_BacksUpCorruptFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-corrupt-bak-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "{\"LibraryRoot\": \"D:\\hanabephoto\\.artifacts\\home-fix-fixture\"}");

            await new AppSettingsStore(path).LoadAsync();

            Directory.GetFiles(directory, "settings.json.corrupt-*").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AcrylicMaterialSetting_DefaultsOnAndSurvivesRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-acrylic-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            new AppSettings().IsAcrylicEnabled.Should().BeTrue();

            var store = new AppSettingsStore(path);
            await store.SaveAsync(new AppSettings { IsAcrylicEnabled = false });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.IsAcrylicEnabled.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CheckDuplicatesOnImport_DefaultsOffAndSurvivesRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-dedup-switch-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            new AppSettings().CheckDuplicatesOnImport.Should().BeFalse();

            var store = new AppSettingsStore(path);
            await store.SaveAsync(new AppSettings { CheckDuplicatesOnImport = true });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.CheckDuplicatesOnImport.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_NormalizesRootRelativeLibraryRootToAbsolutePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-rootfix-save-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            // 根相对路径（无盘符）保存时被规范化：真实 UNC "\\Hanabe\拍照" 可访问时修复为 UNC，
            // 否则回退为当前盘符的绝对路径——结果必须是完全限定路径。
            await new AppSettingsStore(path).SaveAsync(new AppSettings { LibraryRoot = @"\Hanabe\拍照" });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            Path.IsPathFullyQualified(loaded.LibraryRoot!).Should().BeTrue();
            if (Directory.Exists(@"\\Hanabe\拍照"))
            {
                // 真实照片库 UNC 共享可访问：绝不转成本机残留副本（当前盘符的 \Hanabe\拍照）
                loaded.LibraryRoot.Should().Be(@"\\Hanabe\拍照");
                loaded.LibraryRoot.Should().NotBe(Path.GetFullPath(@"\Hanabe\拍照"));
            }
            else
            {
                loaded.LibraryRoot.Should().Be(Path.GetFullPath(@"\Hanabe\拍照"));
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RepairsRootRelativeLibraryRootAndWritesBack()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-rootfix-load-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            // 模拟用户现有坏配置：LibraryRoot = "\Hanabe\拍照"（根相对路径、无盘符）
            await File.WriteAllTextAsync(path, "{\"LibraryRoot\": \"\\\\Hanabe\\\\拍照\"}");

            var loaded = await new AppSettingsStore(path).LoadAsync();

            Path.IsPathFullyQualified(loaded.LibraryRoot!).Should().BeTrue();
            if (Directory.Exists(@"\\Hanabe\拍照"))
            {
                // 真实照片库 UNC 共享可访问：修复为 UNC（绝不转成本机残留副本）
                loaded.LibraryRoot.Should().Be(@"\\Hanabe\拍照");
                loaded.LibraryRoot.Should().NotBe(Path.GetFullPath(@"\Hanabe\拍照"));
            }
            else
            {
                loaded.LibraryRoot.Should().Be(Path.GetFullPath(@"\Hanabe\拍照"));
            }

            // 修复结果已回写 settings.json，二次加载结果稳定
            var reloaded = await new AppSettingsStore(path).LoadAsync();
            reloaded.LibraryRoot.Should().Be(loaded.LibraryRoot);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_LeavesFullyQualifiedLibraryRootUntouched()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-rootfix-keep-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var absolute = Path.Combine(directory, "库");
            Directory.CreateDirectory(absolute);
            await new AppSettingsStore(path).SaveAsync(new AppSettings { LibraryRoot = absolute });

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.LibraryRoot.Should().Be(absolute);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NormalizeLibraryRoot_SingleBackslashRootRelative_PrefersReachableUnc()
    {
        // 根相对路径 "\Hanabe\拍照"（丢失反斜杠的 UNC）：补双反斜杠成 "\\Hanabe\拍照"
        // 且可访问时返回 UNC，绝不 GetFullPath 成 "C:\Hanabe\拍照"（本机 8 月残留副本）。
        var result = AppSettingsStore.NormalizeLibraryRoot(@"\Hanabe\拍照", _ => true);

        result.Should().Be(@"\\Hanabe\拍照");
        result.Should().NotBe(Path.GetFullPath(@"\Hanabe\拍照"));
        Path.IsPathFullyQualified(result!).Should().BeTrue();
    }

    [Fact]
    public void NormalizeLibraryRoot_SingleBackslashRootRelative_FallsBackToDriveAbsoluteWhenUncUnreachable()
    {
        // UNC 候选不可访问时才回退 GetFullPath（当前盘符绝对路径），保证结果仍是完全限定路径。
        var result = AppSettingsStore.NormalizeLibraryRoot(@"\Hanabe\拍照", _ => false);

        var expected = Path.GetFullPath(@"\Hanabe\拍照");
        Path.IsPathFullyQualified(expected).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeLibraryRoot_KeepsFullyQualifiedUncUnchanged()
    {
        // 已完全限定的 UNC（\\server\share）原样保留，不经过 GetFullPath、不被改写。
        AppSettingsStore.NormalizeLibraryRoot(@"\\Hanabe\拍照").Should().Be(@"\\Hanabe\拍照");
        AppSettingsStore.NormalizeLibraryRoot(@"\\Hanabe\拍照\").Should().Be(@"\\Hanabe\拍照");
    }

    [Fact]
    public void NormalizeLibraryRoot_KeepsDriveAbsoluteUnchanged()
    {
        // 回归：盘符绝对路径保持原样。
        AppSettingsStore.NormalizeLibraryRoot(@"C:\photo").Should().Be(@"C:\photo");
        AppSettingsStore.NormalizeLibraryRoot(@"C:\photo\").Should().Be(@"C:\photo");
    }

    [Fact]
    public async Task LoadAsync_RepairsSingleBackslashRootRelativeRootToReachableUncAndWritesBack()
    {
        // 端到端：settings.json 里是根相对路径 "\Hanabe\拍照"（单反斜杠），真实 UNC
        // "\\Hanabe\拍照" 可访问时应修复为 UNC（而非 C 盘路径），并回写 settings.json。
        if (!Directory.Exists(@"\\Hanabe\拍照"))
        {
            return; // 环境无该 UNC 共享时跳过端到端验证（分支逻辑由上面的注入式单测确定性覆盖）
        }

        var directory = Path.Combine(Path.GetTempPath(), "hanabe-unc-repair-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            // JSON 值 "\Hanabe\拍照"（单反斜杠根相对路径，无盘符）
            await File.WriteAllTextAsync(path, "{\"LibraryRoot\": \"\\\\Hanabe\\\\拍照\"}");

            var loaded = await new AppSettingsStore(path).LoadAsync();
            loaded.LibraryRoot.Should().Be(@"\\Hanabe\拍照");

            // 修复结果已回写 settings.json，二次加载仍是 UNC
            var reloaded = await new AppSettingsStore(path).LoadAsync();
            reloaded.LibraryRoot.Should().Be(@"\\Hanabe\拍照");
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
