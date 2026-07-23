using System.Globalization;

namespace HanabePhotoManager.App;

/// <summary>
/// Returns <c>true</c> when the bound value equals the supplied converter parameter
/// (both compared using <see cref="StringComparison.OrdinalIgnoreCase"/>). Used by
/// the preview filter chips to keep a single source of truth in the view model.
/// </summary>
internal sealed class CategoryEqualsConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // The chip is one-way bound: the visual state reflects the current category.
        // Setting CurrentPreviewCategory happens through the view model directly.
        return System.Windows.Data.Binding.DoNothing;
    }
}
