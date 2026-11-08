using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.Mac.Services;

internal sealed class MacThumbnailBackend : IThumbnailBackend
{
    public object? Decode(string path, int width) => ThumbnailService.LoadAsync(path, width).GetAwaiter().GetResult();
    public object? Shell(string path, int width, bool allowExtraction) => null;
}
