using FluentAssertions;
using HanabePhotoManager.Core.Albums;

namespace HanabePhotoManager.Core.Tests.Albums;

public sealed class CustomAlbumTests
{
    [Fact]
    public void Create_NormalizesFolderPathAndUsesFolderNameWhenDisplayNameIsBlank()
    {
        var album = CustomAlbum.Create(Guid.NewGuid(), "  ", @"C:\\Photos\\Summer\\..");

        album.DisplayName.Should().Be("Photos");
        album.FolderPath.Should().Be(Path.GetFullPath(@"C:\\Photos"));
    }
}
