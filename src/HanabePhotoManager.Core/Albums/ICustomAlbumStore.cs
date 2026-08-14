namespace HanabePhotoManager.Core.Albums;

public interface ICustomAlbumStore
{
    Task<IReadOnlyList<CustomAlbum>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyCollection<CustomAlbum> albums, CancellationToken cancellationToken = default);
}
