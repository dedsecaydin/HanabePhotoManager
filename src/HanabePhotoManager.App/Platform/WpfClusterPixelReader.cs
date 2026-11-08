using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Platform;

internal static class WpfClusterPixelReader
{
    public static BgraImagePixels? Read(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.DecodePixelWidth = 96; bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit(); bitmap.Freeze();
        BitmapSource source = bitmap;
        if (source.Format != PixelFormats.Bgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            source.Freeze();
        }
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        return new BgraImagePixels(source.PixelWidth, source.PixelHeight, pixels);
    }
}
