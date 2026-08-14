using FluentAssertions;
using HanabePhotoManager.Core.Albums;
using HanabePhotoManager.Infrastructure.Albums;

namespace HanabePhotoManager.Infrastructure.Tests.Albums;

public sealed class JsonCustomAlbumStoreTests
{
    [Fact]
    public async Task SaveAsync_ReplacesTheVirtualAlbumListAndRoundTripsIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hanabe-custom-albums-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "custom-albums.json");
        try
        {
            var store = new JsonCustomAlbumStore(path);
            var summer = CustomAlbum.Create(Guid.NewGuid(), "夏日精选", @"C:\\Photos\\Summer");

            await store.SaveAsync([summer]);
            await store.SaveAsync([]);

            var loaded = await store.LoadAsync();

            loaded.Should().BeEmpty();
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
