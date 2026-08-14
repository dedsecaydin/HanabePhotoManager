using FluentAssertions;
using HanabePhotoManager.App.Albums;
using HanabePhotoManager.Core.Albums;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.Albums;

public sealed class CustomAlbumsViewModelTests
{
    [Fact]
    public async Task RemoveSelectedAsync_OnlyUpdatesTheVirtualReference()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hanabe-album-vm-" + Guid.NewGuid().ToString("N")));
        try
        {
            var store = new MemoryAlbumStore([CustomAlbum.Create(Guid.NewGuid(), "旅行", directory.FullName)]);
            var viewModel = new CustomAlbumsViewModel(store, new CustomAlbumPhotoScanner());
            await viewModel.InitializeAsync();

            await viewModel.RemoveSelectedAsync();

            viewModel.Albums.Should().BeEmpty();
            store.Albums.Should().BeEmpty();
            Directory.Exists(directory.FullName).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory.FullName)) Directory.Delete(directory.FullName, recursive: true);
        }
    }

    private sealed class MemoryAlbumStore(IReadOnlyList<CustomAlbum> albums) : ICustomAlbumStore
    {
        public IReadOnlyList<CustomAlbum> Albums { get; private set; } = albums;

        public Task<IReadOnlyList<CustomAlbum>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Albums);

        public Task SaveAsync(IReadOnlyCollection<CustomAlbum> albums, CancellationToken cancellationToken = default)
        {
            Albums = albums.ToArray();
            return Task.CompletedTask;
        }
    }
}
