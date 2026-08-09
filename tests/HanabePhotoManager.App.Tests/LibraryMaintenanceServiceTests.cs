using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class LibraryMaintenanceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-maintenance-{Guid.NewGuid():N}");

    [Fact]
    public void IsNetworkLibraryRoot_RecognizesUncPaths()
    {
        LibraryMaintenanceService.IsNetworkLibraryRoot(@"\\Hanabe\拍照").Should().BeTrue();
        LibraryMaintenanceService.IsNetworkLibraryRoot(@"D:\Photos").Should().BeFalse();
    }

    [Fact]
    public async Task RemoveEmptyDateDirectoriesAsync_DeletesEmptyDateAndNestedCategoryFolders()
    {
        var emptyDate = CreateDate("07.01");
        Directory.CreateDirectory(Path.Combine(emptyDate, "JPG生图", "nested"));

        var result = await new LibraryMaintenanceService()
            .RemoveEmptyDateDirectoriesAsync(_root, CancellationToken.None);

        Directory.Exists(emptyDate).Should().BeFalse();
        result.Deleted.Should().Contain(emptyDate);
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveEmptyDateDirectoriesAsync_DeletesDateContainingOnlySystemJunk()
    {
        var junkDate = CreateDate("07.02");
        File.WriteAllText(Path.Combine(junkDate, "Thumbs.db"), "junk");
        File.WriteAllText(Path.Combine(junkDate, "desktop.ini"), "junk");

        await new LibraryMaintenanceService()
            .RemoveEmptyDateDirectoriesAsync(_root, CancellationToken.None);

        Directory.Exists(junkDate).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveEmptyDateDirectoriesAsync_PreservesDateContainingMedia()
    {
        var mediaDate = CreateDate("07.03");
        var category = Directory.CreateDirectory(Path.Combine(mediaDate, "JPG生图")).FullName;
        File.WriteAllBytes(Path.Combine(category, "photo.jpg"), [1, 2, 3]);

        var result = await new LibraryMaintenanceService()
            .RemoveEmptyDateDirectoriesAsync(_root, CancellationToken.None);

        Directory.Exists(mediaDate).Should().BeTrue();
        result.Deleted.Should().NotContain(mediaDate);
    }

    [Fact]
    public async Task RemoveEmptyDateDirectoriesAsync_PreservesUnknownUserContent()
    {
        var contentDate = CreateDate("07.04");
        File.WriteAllText(Path.Combine(contentDate, "拍摄说明.txt"), "keep me");

        await new LibraryMaintenanceService()
            .RemoveEmptyDateDirectoriesAsync(_root, CancellationToken.None);

        Directory.Exists(contentDate).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveEmptyDateDirectoriesAsync_IgnoresNonDateDirectories()
    {
        var unrelated = Directory.CreateDirectory(Path.Combine(_root, "缓存")).FullName;

        await new LibraryMaintenanceService()
            .RemoveEmptyDateDirectoriesAsync(_root, CancellationToken.None);

        Directory.Exists(unrelated).Should().BeTrue();
    }

    private string CreateDate(string name)
    {
        var path = Path.Combine(_root, "2026", "7月", name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
