using Microsoft.UI.Xaml.Controls;
using UltimateFileConverter.WinUI.Services;

namespace UltimateFileConverter.WinUI.Views;

public sealed partial class DependencyDialog : ContentDialog
{
    private readonly DependencyService _service;
    private CancellationTokenSource? _cts;
    private bool _installing;

    public DependencyDialog(DependencyService service)
    {
        InitializeComponent();
        _service = service;
        ToolList.ItemsSource = DependencyService.Required;

        var hasWinget = DependencyService.FindWinget() is not null;
        WingetMissingBar.IsOpen = !hasWinget;
        InstallButton.IsEnabled = hasWinget;

        InstallSummary.Text = _service.AllInstalled()
            ? "All tools are installed."
            : $"{_service.Missing().Count} tool(s) to install.";
    }

    private async void Install_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_installing) return;
        _installing = true;
        _cts = new CancellationTokenSource();

        InstallButton.IsEnabled = false;
        InstallRing.IsActive = true;
        InstallSummary.Text = "Installing…";
        LogContainer.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        LogText.Text = string.Empty;

        var progress = new System.Progress<string>(AppendLog);
        bool ok;
        try
        {
            ok = await _service.InstallMissingAsync(progress, _cts.Token);
        }
        catch (System.OperationCanceledException)
        {
            AppendLog("Setup cancelled.");
            ok = false;
        }
        catch (System.Exception ex)
        {
            AppendLog($"Setup error: {ex.Message}");
            ok = false;
        }

        InstallRing.IsActive = false;
        _installing = false;
        InstallButton.IsEnabled = true;
        InstallButton.Content = "Re-run setup";
        InstallSummary.Text = ok
            ? "All tools are ready."
            : "Some tools are still missing — see the log.";
    }

    private void AppendLog(string line)
    {
        LogText.Text = string.IsNullOrEmpty(LogText.Text) ? line : $"{LogText.Text}\n{line}";
        LogScroller.UpdateLayout();
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null, disableAnimation: true);
    }

    private async void OpenStore_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // App Installer (provides winget) on the Microsoft Store.
        await Windows.System.Launcher.LaunchUriAsync(new System.Uri("ms-windows-store://pdp/?productid=9NBLGGH4NNS1"));
    }

    private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        // Stop any in-flight winget run when the dialog is dismissed.
        _cts?.Cancel();
    }
}
