using System.Globalization;
using System.Windows.Data;

namespace HanabePhotoManager.App;

/// <summary>
/// Formats a byte count (<c>long</c>) into a compact human-readable size
/// ("12.4 MB" / "86.0 KB" / "512 B") for the album list view. View-layer only.
/// </summary>
public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return string.Empty;
        }

        const long kb = 1024L;
        const long mb = 1024L * 1024L;
        return bytes >= mb
            ? $"{bytes / (double)mb:F1} MB"
            : bytes >= kb
                ? $"{bytes / (double)kb:F1} KB"
                : $"{bytes} B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
