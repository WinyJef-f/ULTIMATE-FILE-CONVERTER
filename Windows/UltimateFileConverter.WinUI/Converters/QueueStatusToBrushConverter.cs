using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using UltimateFileConverter.WinUI.Models;
using Windows.UI;

namespace UltimateFileConverter.WinUI.Converters;

/// <summary>Colors the queue status badge: green for done, red for failed, muted otherwise.</summary>
public sealed class QueueStatusToBrushConverter : IValueConverter
{
    // Chosen to read clearly in both light and dark themes.
    private static readonly Brush Success = new SolidColorBrush(Color.FromArgb(0xFF, 0x2F, 0x9E, 0x44));
    private static readonly Brush Failure = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x48, 0x4D));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));

    public object Convert(object value, System.Type targetType, object parameter, string language)
    {
        return value is QueueItemStatus status
            ? status switch
            {
                QueueItemStatus.Done => Success,
                QueueItemStatus.Failed => Failure,
                _ => Muted,
            }
            : Muted;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        => throw new System.NotSupportedException();
}
