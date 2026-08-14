using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App;

/// <summary>
/// Loads a downscaled, frozen thumbnail for a file path string. The optional
/// ConverterParameter controls the decode width in pixels (default 280).
/// Returns null when the path is missing or cannot be decoded, so the host
/// <see cref="System.Windows.Controls.Image"/> can fall back to its placeholder.
/// View-layer only; mirrors the thumbnail strategy already used by the
/// face-search results and the browse preview cards.
/// </summary>
public sealed class PathThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = 280;
        if (parameter is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            width = parsed;
        }

        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

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
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
