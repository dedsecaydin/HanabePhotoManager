namespace HanabePhotoManager.App.Platform;

public interface ISearchPlatform
{
    void Open(string path);
    void Reveal(string path);
    Task<object?> LoadThumbnailAsync(string path, int width, CancellationToken token);
}

internal sealed class NullSearchPlatform : ISearchPlatform
{
    public void Open(string path) { }
    public void Reveal(string path) { }
    public Task<object?> LoadThumbnailAsync(string path, int width, CancellationToken token) => Task.FromResult<object?>(null);
}
