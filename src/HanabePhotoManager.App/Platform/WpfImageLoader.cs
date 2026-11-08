using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Platform;

public sealed class WpfImageFactory : IImageFactory
{
    public object? FromFile(string path, int decodePixelWidth = 0)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    public object? FromEncoded(byte[] data, int decodePixelWidth = 0)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0) image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }
        catch { return null; }
    }

    public object? FromBgra32(int width, int height, byte[] pixels)
    {
        var stride = checked(width * 4);
        if (width <= 0 || height <= 0 || pixels.Length != checked(stride * height)) throw new ArgumentException("Invalid BGRA image dimensions.");
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        image.Freeze();
        return image;
    }

    public Task<object?> LoadThumbnailAsync(string path, int size, CancellationToken token) =>
        Task.Run(() => FromFile(path, size), token);

    public (double Width, double Height)? GetSize(object image) => image is ImageSource source ? (source.Width, source.Height) : null;

    public Task<object?> LoadOrientedThumbnailAsync(string path, int size, CancellationToken token) =>
        Task.Run<object?>(() => LoadThumbnail(path, size), token);

    private static ImageSource? LoadThumbnail(string path, int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            // BitmapImage 不理会 EXIF 方向：竖拍的照片会横着显示、正方形裁切还会把脸切掉。
            return ApplyExifOrientation(image, ReadExifOrientation(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>读取 EXIF 方向（1–8），读不到时按 1（不旋转）。只读文件头。</summary>
    internal static ushort ReadExifOrientation(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            if (decoder.Frames.Count > 0 && decoder.Frames[0].Metadata is BitmapMetadata metadata &&
                metadata.ContainsQuery("System.Photo.Orientation") && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
                return orientation;
        }
        catch (Exception) { /* 没有 EXIF 或格式不支持：按原样显示 */ }
        return 1;
    }

    /// <summary>按 EXIF 方向把缩略图转正（含镜像的 2/4/5/7）。</summary>
    internal static BitmapSource ApplyExifOrientation(BitmapSource source, ushort orientation)
    {
        Transform? transform = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(270) } },
            6 => new RotateTransform(90),
            7 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(90) } },
            8 => new RotateTransform(270),
            _ => null,
        };
        if (transform is null) return source;
        var rotated = new TransformedBitmap(source, transform);
        rotated.Freeze();
        return rotated;
    }
}
