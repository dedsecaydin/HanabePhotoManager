using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Platform;

internal sealed class WpfSearchPlatform : ISearchPlatform
{
    public void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    public void Reveal(string path) => Process.Start("explorer.exe", $"/select,\"{path}\"");
    public Task<object?> LoadThumbnailAsync(string path, int width, CancellationToken token) => Task.Run<object?>(() =>
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }, token);
}
