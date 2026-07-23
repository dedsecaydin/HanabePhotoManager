using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MediaMetadataStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-metadata-{Guid.NewGuid():N}");

    [Fact]
    public async Task UpsertAsync_RoundTripsLabelsTagsPeopleAndLocation()
    {
        var storePath = Path.Combine(_directory, "media-metadata.json");
        var mediaPath = Path.Combine(_directory, "Photo.JPG");
        var store = new MediaMetadataStore(storePath);
        var entry = new MediaMetadataEntry
        {
            Path = mediaPath,
            Fingerprint = "10:12345",
            AutomaticLabels = [new PhotoLabelScore("风景", 0.82)],
            ManualCategory = "旅行",
            ManualTags = ["海边", "夏天"],
            PeopleIds = ["person-a"],
            ExifLocation = new PhotoLocation(36.06, 120.38, PhotoLocationSource.Exif),
            ManualLocation = new PhotoLocation(31.23, 121.47, PhotoLocationSource.Manual)
        };

        await store.UpsertAsync(entry);
        var restored = await new MediaMetadataStore(storePath).GetAsync(mediaPath);

        restored.Should().NotBeNull();
        restored!.Path.Should().Be(Path.GetFullPath(mediaPath));
        restored.EffectiveCategory.Should().Be("旅行");
        restored.ManualTags.Should().BeEquivalentTo("海边", "夏天");
        restored.EffectiveLocation.Should().Be(restored.ManualLocation);
    }

    [Fact]
    public async Task LoadAsync_CorruptDocumentReturnsEmptyAndPreservesBackup()
    {
        Directory.CreateDirectory(_directory);
        var storePath = Path.Combine(_directory, "media-metadata.json");
        await File.WriteAllTextAsync(storePath, "{ broken json");

        var snapshot = await new MediaMetadataStore(storePath).LoadAsync();

        snapshot.Entries.Should().BeEmpty();
        Directory.EnumerateFiles(_directory, "media-metadata.corrupt-*.json").Should().ContainSingle();
    }

    [Fact]
    public async Task UpsertAsync_ReplacesSamePathCaseInsensitivelyAndLeavesNoTemporaryFile()
    {
        var storePath = Path.Combine(_directory, "media-metadata.json");
        var store = new MediaMetadataStore(storePath);
        await store.UpsertAsync(new MediaMetadataEntry { Path = Path.Combine(_directory, "A.JPG"), ManualCategory = "人像" });
        await store.UpsertAsync(new MediaMetadataEntry { Path = Path.Combine(_directory, "a.jpg"), ManualCategory = "夜景" });

        var snapshot = await store.LoadAsync();

        snapshot.Entries.Should().ContainSingle().Which.EffectiveCategory.Should().Be("夜景");
        File.Exists(storePath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void EffectiveValues_PreferManualDecisionsOverAutomaticMetadata()
    {
        var entry = new MediaMetadataEntry
        {
            AutomaticLabels = [new PhotoLabelScore("风景", 0.99)],
            ManualCategory = "自定义类别",
            ExifLocation = new PhotoLocation(1, 2, PhotoLocationSource.Exif),
            ManualLocation = new PhotoLocation(3, 4, PhotoLocationSource.Manual)
        };

        entry.EffectiveCategory.Should().Be("自定义类别");
        entry.EffectiveLocation.Should().Be(entry.ManualLocation);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
