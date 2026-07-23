using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PhotoLocationServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-location-{Guid.NewGuid():N}");

    [Fact]
    public async Task ManualAssignmentOverridesExifAndClearFallsBackToExif()
    {
        var path = Path.Combine(_directory, "a.jpg");
        var store = CreateStore();
        await store.UpsertAsync(new MediaMetadataEntry
        {
            Path = path,
            ExifLocation = new PhotoLocation(36, 120, PhotoLocationSource.Exif)
        });
        var service = new PhotoLocationService(store);

        await service.AssignManualAsync([path], 31.23, 121.47, "上海", default);
        (await service.GetEffectiveAsync(path, default))!.Source.Should().Be(PhotoLocationSource.Manual);
        await service.ClearManualAsync([path], default);

        var restored = await service.GetEffectiveAsync(path, default);
        restored.Should().NotBeNull();
        restored!.Latitude.Should().Be(36);
        restored.Source.Should().Be(PhotoLocationSource.Exif);
    }

    [Fact]
    public async Task BatchAssignmentAndLocatedFiltering_AreDeterministic()
    {
        var paths = new[] { Path.Combine(_directory, "a.jpg"), Path.Combine(_directory, "b.jpg") };
        var service = new PhotoLocationService(CreateStore());
        await service.AssignManualAsync(paths, 10, 20, null, default);

        var located = await service.GetLocatedAsync([.. paths, Path.Combine(_directory, "none.jpg")], default);

        located.Should().HaveCount(2);
        located.Select(item => item.Path).Should().BeEquivalentTo(paths);
    }

    [Fact]
    public void Cluster_UsesStableGridCellsAndCounts()
    {
        var points = new[]
        {
            new LocatedPhoto("a", new PhotoLocation(30.000, 120.000, PhotoLocationSource.Exif)),
            new LocatedPhoto("b", new PhotoLocation(30.004, 120.004, PhotoLocationSource.Manual)),
            new LocatedPhoto("c", new PhotoLocation(31.000, 121.000, PhotoLocationSource.Exif))
        };

        var clusters = PhotoLocationService.Cluster(points, zoom: 10);

        clusters.Should().HaveCount(2);
        clusters.Select(cluster => cluster.Count).Should().BeEquivalentTo([2, 1]);
        clusters.Should().BeInAscendingOrder(cluster => cluster.Key);
    }

    private MediaMetadataStore CreateStore() => new(Path.Combine(_directory, "metadata.json"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
