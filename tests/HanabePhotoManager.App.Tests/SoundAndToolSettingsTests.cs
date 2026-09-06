using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class SoundAndToolSettingsTests
{
    [Fact]
    public async Task EventPreferencesAndToolDefaults_SurviveReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabeSettings", Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "settings.json");
            await new AppSettingsStore(path).SaveAsync(new AppSettings
            {
                HanabeSoundStartEnabled = false, HanabeSoundCompletionEnabled = false,
                HanabeSoundFailureEnabled = false, HanabeSoundSkipEnabled = false,
                HanabeSoundOpenEnabled = true, HanabeSoundCancelEnabled = false,
                DefaultCompressionMegabytes = 4.5, DefaultCollageBlur = true, DefaultCollageLimitSize = true,
                DefaultWatermarkOpacity = .4, DefaultWatermarkPreserveMetadata = false, DefaultWatermarkRecursive = false
            });
            var loaded = await new AppSettingsStore(path).LoadAsync(default);
            loaded.HanabeSoundStartEnabled.Should().BeFalse();
            loaded.HanabeSoundCompletionEnabled.Should().BeFalse();
            loaded.HanabeSoundFailureEnabled.Should().BeFalse();
            loaded.HanabeSoundSkipEnabled.Should().BeFalse();
            loaded.HanabeSoundOpenEnabled.Should().BeTrue();
            loaded.HanabeSoundCancelEnabled.Should().BeFalse();
            loaded.DefaultCompressionMegabytes.Should().Be(4.5);
            loaded.DefaultCollageBlur.Should().BeTrue();
            loaded.DefaultCollageLimitSize.Should().BeTrue();
            loaded.DefaultWatermarkOpacity.Should().Be(.4);
            loaded.DefaultWatermarkPreserveMetadata.Should().BeFalse();
            loaded.DefaultWatermarkRecursive.Should().BeFalse();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
