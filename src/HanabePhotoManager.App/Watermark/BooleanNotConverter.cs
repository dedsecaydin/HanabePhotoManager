using System.Globalization;
using System.Windows.Data;

namespace HanabePhotoManager.App.Watermark;

/// <summary>供 XAML 绑定使用的布尔值取反转换器。</summary>
public sealed class BooleanNotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool flag && !flag;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool flag && !flag;
}
