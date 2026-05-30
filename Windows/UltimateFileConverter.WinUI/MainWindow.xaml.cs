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
        SetWindowIcon();

        _rootView = new MainView();
        Content = _rootView;
        _rootView.Initialize(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    private void SetWindowIcon()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
            var ico = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(ico)) appWindow.SetIcon(ico);
        }
        catch
        {
            // Non-fatal if the icon can't be applied.
        }
    }
}
