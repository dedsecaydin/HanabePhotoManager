using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HanabePhotoManager.App;

/// <summary>
/// Converts an object reference to <see cref="Visibility"/>.
/// By default a non-null value yields <see cref="Visibility.Visible"/> and null
/// yields <see cref="Visibility.Collapsed"/>; pass "Invert" to reverse that.
/// Used by the people page to switch between the album overview and the
/// selected-person detail purely in the View layer.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var isNull = value is null;
        var show = invert ? isNull : !isNull;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
