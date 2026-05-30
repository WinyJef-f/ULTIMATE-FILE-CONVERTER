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

    private void ConfigureAppWindow()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

            var ico = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(ico)) appWindow.SetIcon(ico);

            // Lock the window to a 900×900 logical-pixel square, scaled for display DPI.
            var dpi = GetDpiForWindow(hwnd);
            var side = (int)(900 * dpi / 96.0);
            appWindow.Resize(new Windows.Graphics.SizeInt32(side, side));

            // Center on the nearest display.
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var work = displayArea.WorkArea;
            appWindow.Move(new Windows.Graphics.PointInt32(
                work.X + (work.Width - side) / 2,
                work.Y + (work.Height - side) / 2));

            // Prevent the user from resizing to a non-square or off-center state.
            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.IsResizable = false;
        }
        catch
        {
            // Non-fatal; window setup is best-effort.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr hWnd);
}
