using Microsoft.UI.Xaml.Controls;
using UltimateFileConverter.WinUI.Models;
using Windows.Storage.Pickers;

namespace UltimateFileConverter.WinUI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly System.IntPtr _windowHandle;
    private readonly System.Action _onClearFullHistory;
    private string? _customFolderPath;

    /// <summary>The edited settings. Read this only when <see cref="ContentDialog.ShowAsync"/> returns Primary.</summary>
    public ConversionSettings Result { get; private set; }

    public SettingsDialog(ConversionSettings current, System.IntPtr windowHandle,
                          int fullHistoryCount, System.Action onClearFullHistory)
    {
        InitializeComponent();
        _windowHandle = windowHandle;
        _onClearFullHistory = onClearFullHistory;
        Result = current.Clone();
        _customFolderPath = current.CustomOutputFolderPath;

        QualitySlider.Value = current.ImageQuality;
        CrfSlider.Value = current.VideoCRF;
        SelectComboByTag(BitrateCombo, current.AudioBitrate.ToString(), fallbackIndex: 2);
        SelectComboByTag(OutputModeCombo, current.OutputFolderMode.ToString(), fallbackIndex: 0);
        FolderPathText.Text = string.IsNullOrWhiteSpace(_customFolderPath) ? "No folder chosen" : _customFolderPath;
        ExperimentalToggle.IsOn = current.WeirdModeEnabled;
        ExperimentalWarning.IsOpen = current.WeirdModeEnabled;
        FullHistoryCountLabel.Text = FormatCount(fullHistoryCount);

        PrimaryButtonClick += (_, _) => Result = BuildSettings();
    }

    private ConversionSettings BuildSettings() => new()
    {
        ImageQuality = (int)System.Math.Round(QualitySlider.Value),
        AudioBitrate = SelectedTagInt(BitrateCombo, 192),
        VideoCRF = (int)System.Math.Round(CrfSlider.Value),
        OutputFolderMode = SelectedTag(OutputModeCombo) == "CustomFolder"
            ? OutputFolderMode.CustomFolder
            : OutputFolderMode.NextToSource,
        CustomOutputFolderPath = _customFolderPath,
        WeirdModeEnabled = ExperimentalToggle.IsOn,
    };

    private void QualitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (QualityValue is not null) QualityValue.Text = ((int)System.Math.Round(e.NewValue)).ToString();
    }

    private void CrfSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (CrfValue is not null) CrfValue.Text = ((int)System.Math.Round(e.NewValue)).ToString();
    }

    private void OutputModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomFolderPanel is null) return;
        CustomFolderPanel.Visibility = SelectedTag(OutputModeCombo) == "CustomFolder"
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private void ExperimentalToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ExperimentalWarning is not null) ExperimentalWarning.IsOpen = ExperimentalToggle.IsOn;
    }

    private async void ChooseFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            _customFolderPath = folder.Path;
            FolderPathText.Text = folder.Path;
        }
    }

    private void ClearFullHistory_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // ContentDialog can't host a second ContentDialog, so use a Flyout for confirmation.
        var panel = new StackPanel { Spacing = 10, Width = 230 };
        panel.Children.Add(new TextBlock
        {
            Text = "This permanently deletes all conversion records and resets your stats. Recent history in the main view is not affected.",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            FontSize = 12,
        });
        var confirmBtn = new Button
        {
            Content = "Yes, clear history",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        panel.Children.Add(confirmBtn);

        var flyout = new Flyout { Content = panel };
        confirmBtn.Click += (_, _) =>
        {
            flyout.Hide();
            _onClearFullHistory();
            FullHistoryCountLabel.Text = FormatCount(0);
        };
        flyout.ShowAt(ClearFullHistoryButton);
    }

    private static string FormatCount(int count) =>
        $"{count} conversion{(count == 1 ? "" : "s")} recorded";

    private static void SelectComboByTag(ComboBox combo, string tag, int fallbackIndex)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is string t && t == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = System.Math.Min(fallbackIndex, combo.Items.Count - 1);
    }

    private static string SelectedTag(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;

    private static int SelectedTagInt(ComboBox combo, int fallback)
        => int.TryParse(SelectedTag(combo), out var value) ? value : fallback;
}
