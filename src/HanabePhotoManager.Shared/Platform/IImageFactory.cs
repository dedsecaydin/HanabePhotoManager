namespace HanabePhotoManager.App.Platform;

public interface IImageFactory
{
    object? FromFile(string path, int decodePixelWidth = 0);
    object? FromEncoded(byte[] data, int decodePixelWidth = 0);
    object? FromBgra32(int width, int height, byte[] pixels);
    Task<object?> LoadThumbnailAsync(string path, int size, CancellationToken token);
    Task<object?> LoadOrientedThumbnailAsync(string path, int size, CancellationToken token);
    (double Width, double Height)? GetSize(object image);
}

internal sealed class NullImageFactory : IImageFactory
{
    public object? FromFile(string path, int decodePixelWidth = 0) => null;
    public object? FromEncoded(byte[] data, int decodePixelWidth = 0) => null;
    public object? FromBgra32(int width, int height, byte[] pixels) => null;
    public Task<object?> LoadThumbnailAsync(string path, int size, CancellationToken token) => Task.FromResult<object?>(null);
    public Task<object?> LoadOrientedThumbnailAsync(string path, int size, CancellationToken token) => Task.FromResult<object?>(null);
    public (double Width, double Height)? GetSize(object image) => null;
}
