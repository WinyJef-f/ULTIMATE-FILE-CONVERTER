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
            UnhandledException += (_, e) => { ReportFatal("UI: " + e.Message, e.Exception); e.Handled = true; };

            // When a {StaticResource}/{ThemeResource} key or an x:Bind path can't be resolved,
            // XAML surfaces only a generic XamlParseException (0x802B000A). These trace events
            // fire *during* the parse and name the exact key/binding, so a future failure lands
            // in startup-error.log with something actionable instead of a generic code.
            try
            {
                DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
                DebugSettings.IsBindingTracingEnabled = true;
                DebugSettings.XamlResourceReferenceFailed += (_, e) => AppendLog("XamlResourceReferenceFailed", e.Message);
                DebugSettings.BindingFailed += (_, e) => AppendLog("BindingFailed", e.Message);
            }
            catch
            {
                // Tracing is best-effort diagnostics only.
            }
        }
        catch (System.Exception ex)
        {
            ReportFatal("App.InitializeComponent", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LogStartupEnvironment();
        try
        {
            // Single-instance: redirect any second launch to the already-running window.
            var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("ufc-main");
            if (!mainInstance.IsCurrent)
            {
                mainInstance.RedirectActivationToAsync(
                    Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs()
                ).AsTask().GetAwaiter().GetResult();
                System.Environment.Exit(0);
                return;
            }
            mainInstance.Activated += OnInstanceActivated;

            _window = new MainWindow();
            _window.Activate();

            // Handle files passed on this initial launch (e.g. from right-click context menu).
            var cmdArgs = System.Environment.GetCommandLineArgs();
            if (cmdArgs.Length > 1 && _window is MainWindow mw)
            {
                var files = cmdArgs.Skip(1)
                    .Select(p => p.Trim('"'))
                    .Where(p => System.IO.File.Exists(p))
                    .ToArray();
                if (files.Length > 0)
                    mw.EnqueueFiles(files);
            }
        }
        catch (System.Exception ex)
        {
            ReportFatal("OnLaunched", ex);
            throw;
        }
    }

    private void OnInstanceActivated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments e)
    {
        _window?.DispatcherQueue?.TryEnqueue(() =>
        {
            _window?.Activate();
            if (e.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs
                && _window is MainWindow mw)
            {
                var files = ParseArguments(launchArgs.Arguments)
                    .Where(p => System.IO.File.Exists(p))
                    .ToArray();
                if (files.Length > 0)
                    mw.EnqueueFiles(files);
            }
        });
    }

    private static string[] ParseArguments(string commandLine)
    {
        var results = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in commandLine)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ' ' && !inQuotes) { if (current.Length > 0) { results.Add(current.ToString()); current.Clear(); } }
            else current.Append(c);
        }
        if (current.Length > 0) results.Add(current.ToString());
        return results.ToArray();
    }

    private static void LogStartupEnvironment()
    {
        try
        {
            var base_ = System.AppContext.BaseDirectory;
            var pri = System.IO.Path.Combine(base_, "resources.pri");
            var priInfo = System.IO.File.Exists(pri)
                ? $"EXISTS ({new System.IO.FileInfo(pri).Length:N0} bytes)"
                : "MISSING";
            AppendLog("Startup", $"BaseDirectory: {base_}\nresources.pri: {priInfo}\nOS: {System.Environment.OSVersion}");
        }
        catch { }
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

        AppendLog(source, detail);

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

    /// <summary>Appends a timestamped entry to %LOCALAPPDATA%\ULTIMATE-FILE-CONVERTER\startup-error.log.</summary>
    private static void AppendLog(string source, string detail)
    {
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
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(System.IntPtr hWnd, string text, string caption, uint type);
}
