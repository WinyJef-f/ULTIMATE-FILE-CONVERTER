using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Views;

public sealed partial class StatsDialog : ContentDialog
{
    public StatsDialog(ConversionStats stats)
    {
        InitializeComponent();

        SuccessText.Text = stats.TotalSuccessesText;
        FailedText.Text = stats.TotalFailuresText;
        BytesText.Text = stats.FormattedBytes;

        var hasAny = stats.TotalSuccesses > 0 || stats.TotalFailures > 0;
        EmptyStatePanel.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;

        if (stats.TopFormatsItems.Count > 0)
        {
            TopFormatsList.ItemsSource = stats.TopFormatsItems;
            TopFormatsPanel.Visibility = Visibility.Visible;
        }
        else
        {
            TopFormatsPanel.Visibility = Visibility.Collapsed;
        }

        if (stats.TopPairsItems.Count > 0)
        {
            TopPairsList.ItemsSource = stats.TopPairsItems;
            TopPairsPanel.Visibility = Visibility.Visible;
        }
        else
        {
            TopPairsPanel.Visibility = Visibility.Collapsed;
        }
    }
}
