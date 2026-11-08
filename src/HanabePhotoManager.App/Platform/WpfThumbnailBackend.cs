using System.IO;
using System.Windows.Media.Imaging;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.Platform;

internal sealed class WpfThumbnailBackend : IThumbnailBackend
{
    public object? Decode(string path, int width)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width; image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }
        catch { return null; }
    }
    public object? Shell(string path, int width, bool allowExtraction) => ShellThumbnailProvider.TryGetThumbnail(path, width, allowExtraction);
}
