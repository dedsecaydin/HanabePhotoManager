using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.Services;

internal sealed class AvaloniaImageFactory : IImageFactory
{
    public object? FromFile(string path, int decodePixelWidth = 0)
    {
        try { using var stream = File.OpenRead(path); return Decode(stream, decodePixelWidth); }
        catch (Exception) { return null; }
    }

    public object? FromEncoded(byte[] data, int decodePixelWidth = 0)
    {
        try { using var stream = new MemoryStream(data, writable: false); return Decode(stream, decodePixelWidth); }
        catch (Exception) { return null; }
    }

    public object? FromBgra32(int width, int height, byte[] pixels)
    {
        var stride = checked(width * 4);
        if (width <= 0 || height <= 0 || pixels.Length != checked(stride * height)) throw new ArgumentException("Invalid BGRA image dimensions.");
        var image = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var buffer = image.Lock();
        for (var y = 0; y < height; y++) Marshal.Copy(pixels, y * stride, buffer.Address + y * buffer.RowBytes, stride);
        return image;
    }

    public async Task<object?> LoadThumbnailAsync(string path, int size, CancellationToken token) =>
        await ThumbnailService.LoadAsync(path, size, token);
    public Task<object?> LoadOrientedThumbnailAsync(string path, int size, CancellationToken token) => LoadThumbnailAsync(path, size, token);
    public (double Width, double Height)? GetSize(object image) => image is Bitmap bitmap ? (bitmap.Size.Width, bitmap.Size.Height) : null;

    private static Bitmap Decode(Stream stream, int width) => width > 0 ? Bitmap.DecodeToWidth(stream, width) : new Bitmap(stream);
}
