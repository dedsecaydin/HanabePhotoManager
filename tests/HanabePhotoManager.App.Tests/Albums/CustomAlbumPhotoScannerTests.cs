using FluentAssertions;
using HanabePhotoManager.App.Albums;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.Albums;

public sealed class CustomAlbumPhotoScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsSupportedImagesFromNestedFolders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-album-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(directory, "nested"));
            await File.WriteAllBytesAsync(Path.Combine(directory, "cover.jpg"), []);
            await File.WriteAllBytesAsync(Path.Combine(nested.FullName, "detail.PNG"), []);
            await File.WriteAllBytesAsync(Path.Combine(directory, "notes.txt"), []);

            var photos = await new CustomAlbumPhotoScanner().ScanAsync(directory);

            photos.Select(photo => photo.Name).Should().BeEquivalentTo("cover.jpg", "detail.PNG");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
