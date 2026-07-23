using System.Globalization;
using System.Windows.Data;

namespace HanabePhotoManager.App;

/// <summary>
/// Accepts two values at indices 0 and 1 and returns <c>true</c> when they are
/// equal (OrdinalIgnoreCase string comparison). Used by the preview category
/// filter chips to highlight the active category without relying on a
/// non-bindable <see cref="Binding.ConverterParameter"/>.
/// </summary>
internal sealed class CategoryEqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [string first, string second])
        {
            return false;
        }

        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
