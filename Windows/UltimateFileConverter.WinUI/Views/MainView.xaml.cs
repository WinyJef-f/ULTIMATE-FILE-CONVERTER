using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UltimateFileConverter.WinUI.Models;
using UltimateFileConverter.WinUI.Services;
using UltimateFileConverter.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UltimateFileConverter.WinUI.Views;

/// <summary>
/// The whole app surface, hosted by <see cref="MainWindow"/>. Lives in a UserControl
/// (a FrameworkElement) rather than directly on the Window so x:Bind works — a WinUI 3
/// Window is not a FrameworkElement.
/// </summary>
public sealed partial class MainView : UserControl
{
    private readonly DependencyService _dependencyService = new();
    private System.IntPtr _windowHandle;
    private bool _checkedFirstRun;

    public MainViewModel ViewModel { get; } = new();

    public MainView()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebuildTargetMenu();
        Loaded += OnLoaded;
    }

    /// <summary>Receives the host window handle (needed by file/folder pickers).</summary>
    public void Initialize(System.IntPtr windowHandle) => _windowHandle = windowHandle;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_checkedFirstRun) return;
        _checkedFirstRun = true;

        if (!_dependencyService.ShouldOfferFirstRunSetup()) return;
        try
        {
            var dialog = new DependencyDialog(_dependencyService) { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
        }
        catch
        {
            // First-run setup is optional; ignore failures to present it.
        }
    }

    // MARK: - Adding files

    private async void AddFiles_Click(object sender, RoutedEventArgs e) => await AddFilesViaPickerAsync();

    private async void DropZone_Tapped(object sender, TappedRoutedEventArgs e) => await AddFilesViaPickerAsync();

    private async void AddFilesAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddFilesViaPickerAsync();
    }

    private async void ConvertAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.CanConvert) await ViewModel.ConvertAsync();
    }

    private async System.Threading.Tasks.Task AddFilesViaPickerAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
        {
            ViewModel.AddFiles(files.Select(f => f.Path).Where(p => !string.IsNullOrEmpty(p)));
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            if (e.DragUIOverride is not null)
            {
                e.DragUIOverride.Caption = "Add to queue";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
            }
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.OfType<StorageFile>().Select(f => f.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count > 0) ViewModel.AddFiles(paths);
        }
        finally
        {
            deferral.Complete();
        }
    }

    // MARK: - Toolbar actions

    private async void Convert_Click(object sender, RoutedEventArgs e) => await ViewModel.ConvertAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.CancelConversion();

    private void ClearQueue_Click(object sender, RoutedEventArgs e) => ViewModel.ClearQueue();

    private void ClearFinished_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFinished();

    private void ClearHistory_Click(object sender, RoutedEventArgs e) => ViewModel.ClearHistory();

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(
            ViewModel.Settings, _windowHandle,
            ViewModel.FullHistoryCount, ViewModel.ClearFullHistory)
        { XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.ApplySettings(dialog.Result);
        }
    }

    private async void Stats_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new StatsDialog(ViewModel.Stats) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    private async void Tools_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DependencyDialog(_dependencyService) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    // MARK: - Per-row actions

    private void QueueItemMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not QueueItem item) return;

        var flyout = new MenuFlyout();

        if (item.IsDone && !string.IsNullOrEmpty(item.OutputPath))
        {
            var reveal = new MenuFlyoutItem { Text = "Show in File Explorer" };
            reveal.Click += (_, _) => RevealInExplorer(item.OutputPath!);
            flyout.Items.Add(reveal);

            var open = new MenuFlyoutItem { Text = "Open" };
            open.Click += (_, _) => OpenPath(item.OutputPath!);
            flyout.Items.Add(open);

            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        if (item.IsFailed && !string.IsNullOrEmpty(item.ErrorMessage))
        {
            var copy = new MenuFlyoutItem { Text = "Copy error message" };
            copy.Click += (_, _) => CopyText(item.ErrorMessage!);
            flyout.Items.Add(copy);

            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        var remove = new MenuFlyoutItem { Text = "Remove from queue" };
        remove.Click += (_, _) => ViewModel.Remove(item);
        flyout.Items.Add(remove);

        flyout.ShowAt(button);
    }

    private void HistoryReveal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HistoryEntry entry && !string.IsNullOrEmpty(entry.OutputPath))
        {
            RevealInExplorer(entry.OutputPath!);
        }
    }

    // MARK: - Target picker menu

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.ValidTargetGroups)
            or nameof(MainViewModel.TargetFormat)
            or nameof(MainViewModel.ValidTargets))
        {
            RebuildTargetMenu();
        }
    }

    private void RebuildTargetMenu()
    {
        var groups = ViewModel.ValidTargetGroups;
        if (groups.Count == 0)
        {
            TargetPickerButton.Flyout = null;
            return;
        }

        var flyout = new MenuFlyout();
        var first = true;
        foreach (var group in groups)
        {
            if (!first) flyout.Items.Add(new MenuFlyoutSeparator());
            first = false;

            flyout.Items.Add(new MenuFlyoutItem { Text = group.Header, IsEnabled = false });
            foreach (var kind in group.Items)
            {
                var captured = kind;
                var item = new ToggleMenuFlyoutItem
                {
                    Text = kind.DisplayName(),
                    IsChecked = ViewModel.TargetFormat == kind,
                };
                item.Click += (_, _) => ViewModel.TargetFormat = captured;
                flyout.Items.Add(item);
            }
        }
        TargetPickerButton.Flyout = flyout;
    }

    // MARK: - Shell helpers

    private static void RevealInExplorer(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (dir is not null && System.IO.Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                }
            }
        }
        catch
        {
            // Ignore shell failures.
        }
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Ignore shell failures.
        }
    }

    private static void CopyText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch
        {
            // Ignore clipboard failures.
        }
    }
}
