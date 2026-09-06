using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DuplicateSettingsTests
{
    [Fact]
    public async Task DetailedControls_RoundTripAcrossRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabeDuplicateSettings", Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "settings.json");
            await new AppSettingsStore(path).SaveAsync(new AppSettings
            {
                UseDuplicateHashCache = false, DuplicateHashParallelism = 3, VisualDuplicateThreshold = 4,
                ShowSimilarityDifferenceGrid = false, PromptImportResumeAtStartup = false
            });
            var loaded = await new AppSettingsStore(path).LoadAsync(default);
            loaded.UseDuplicateHashCache.Should().BeFalse();
            loaded.DuplicateHashParallelism.Should().Be(3);
            loaded.VisualDuplicateThreshold.Should().Be(4);
            loaded.ShowSimilarityDifferenceGrid.Should().BeFalse();
            loaded.PromptImportResumeAtStartup.Should().BeFalse();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
