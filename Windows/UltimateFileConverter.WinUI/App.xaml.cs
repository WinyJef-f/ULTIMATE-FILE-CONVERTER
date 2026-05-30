using Microsoft.UI.Xaml;

namespace UltimateFileConverter.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        // Catch failures that happen off the UI dispatcher (and before the window exists).
        System.AppDomain.CurrentDomain.UnhandledException +=
            (_, e) => ReportFatal("AppDomain", e.ExceptionObject as System.Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException +=
            (_, e) => { ReportFatal("Task", e.Exception); e.SetObserved(); };

        try
        {
            InitializeComponent();
            UnhandledException += (_, e) => { ReportFatal("UI", e.Exception); e.Handled = true; };
        }
        catch (System.Exception ex)
        {
            ReportFatal("App.InitializeComponent", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (System.Exception ex)
        {
            ReportFatal("OnLaunched", ex);
            throw;
        }
    }

    /// <summary>
    /// Records an otherwise-silent startup/runtime failure to a log file and shows a
    /// message box, so a crash isn't an invisible "nothing happened".
    /// </summary>
    private static void ReportFatal(string source, System.Exception? ex)
    {
        var detail = ex is not null
            ? $"HResult: 0x{ex.HResult:X8}\n\n{ex}"
            : "(no exception object)";

        try
        {
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "ULTIMATE-FILE-CONVERTER");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "startup-error.log"),
                $"[{System.DateTimeOffset.Now:O}] ({source})\n{detail}\n\n");
        }
        catch
        {
            // Logging is best-effort.
        }

        try
        {
            var message = detail.Length > 1500 ? detail[..1500] + "\n…" : detail;
            MessageBoxW(System.IntPtr.Zero, message,
                "ULTIMATE-FILE-CONVERTER failed to start", 0x10 /* MB_ICONERROR */);
        }
        catch
        {
            // If even the message box fails, the log above is the fallback.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(System.IntPtr hWnd, string text, string caption, uint type);
}
