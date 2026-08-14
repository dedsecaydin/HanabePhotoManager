using System.Globalization;

namespace HanabePhotoManager.App;

/// <summary>
/// Converts a file extension string (e.g. "jpg") into an uppercase display
/// label such as "JPG" or "PNG" for badge overlays.
/// </summary>
internal sealed class ExtensionBadgeConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string ext || ext.Length == 0)
        {
            return string.Empty;
        }

        return ext.ToUpperInvariant();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}