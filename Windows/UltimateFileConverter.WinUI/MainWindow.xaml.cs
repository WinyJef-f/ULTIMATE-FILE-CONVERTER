using Microsoft.UI.Xaml;
using UltimateFileConverter.WinUI.Views;

namespace UltimateFileConverter.WinUI;

public sealed partial class MainWindow : Window
{
    // MainView is created programmatically rather than via <views:MainView> in XAML.
    // Relying on the WinUI 3 XAML parser to activate a UserControl from a custom sub-namespace
    // requires the CsWinRT activation factory to be registered before LoadComponent runs;
    // creating it in C# bypasses that activation path entirely.
    private readonly MainView _rootView;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ULTIMATE-FILE-CONVERTER";
        ConfigureAppWindow();

        _rootView = new MainView();
        Content = _rootView;
        _rootView.Initialize(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    /// <summary>Enqueues files into the conversion queue — called by App on activation.</summary>
    public void EnqueueFiles(System.Collections.Generic.IEnumerable<string> paths)
    {
        _rootView.ViewModel.AddFiles(paths);
    }

    private void ConfigureAppWindow()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

            var ico = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(ico)) appWindow.SetIcon(ico);

            // Match the macOS window proportions: ideal 760×680, resizable down to 640×520.
            // Sizes are in physical pixels, so scale by the display DPI.
            var dpi = GetDpiForWindow(hwnd);
            var scale = dpi / 96.0;
            var width = (int)(760 * scale);
            var height = (int)(680 * scale);
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

            // Center on the nearest display.
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var work = displayArea.WorkArea;
            appWindow.Move(new Windows.Graphics.PointInt32(
                work.X + (work.Width - width) / 2,
                work.Y + (work.Height - height) / 2));

            // Resizable, mirroring the macOS minimum window size.
            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
            }
        }
        catch
        {
            // Non-fatal; window setup is best-effort.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr hWnd);
}
