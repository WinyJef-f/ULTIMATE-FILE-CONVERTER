using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace UltimateFileConverter.WinUI.Converters;

/// <summary>
/// Maps a <see cref="bool"/> to <see cref="Visibility"/>. Pass ConverterParameter="Invert"
/// to show on <c>false</c> instead of <c>true</c>.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("Invert", System.StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, string language)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        if (parameter is string s && s.Equals("Invert", System.StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }
        return visible;
    }
}
