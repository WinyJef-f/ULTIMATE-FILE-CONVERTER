using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using UltimateFileConverter.WinUI.Models;
using Windows.UI;

namespace UltimateFileConverter.WinUI.Converters;

/// <summary>Colors the queue status badge: green for done, red for failed, muted otherwise.</summary>
public sealed class QueueStatusToBrushConverter : IValueConverter
{
    // Do NOT use static fields for SolidColorBrush — it is a WinRT DependencyObject and its
    // static initializer can fire before the XAML framework is fully active, causing a
    // XamlParseException with no inner detail at the COM boundary. Resolve via ThemeResources
    // at call time instead; fall back to explicit colors for the very first call.
    public object Convert(object value, System.Type targetType, object parameter, string language)
    {
        var key = value is QueueItemStatus status
            ? status switch
            {
                QueueItemStatus.Done    => "SystemFillColorSuccessBrush",
                QueueItemStatus.Failed  => "SystemFillColorCriticalBrush",
                _                       => "TextFillColorSecondaryBrush",
            }
            : "TextFillColorSecondaryBrush";

        if (Application.Current?.Resources?.TryGetValue(key, out var res) == true && res is Brush b)
            return b;

        return MakeBrush(value);
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        => throw new System.NotSupportedException();

    private static Brush MakeBrush(object value)
    {
        if (value is QueueItemStatus s)
        {
            return s switch
            {
                QueueItemStatus.Done   => new SolidColorBrush(Color.FromArgb(0xFF, 0x2F, 0x9E, 0x44)),
                QueueItemStatus.Failed => new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x48, 0x4D)),
                _                      => new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A)),
            };
        }
        return new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));
    }
}
