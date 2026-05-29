using Microsoft.UI.Xaml;

namespace UltimateFileConverter.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "ULTIMATE-FILE-CONVERTER";
        SetWindowIcon();
        RootView.Initialize(WinRT.Interop.WindowNative.GetWindowHandle(this));
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
