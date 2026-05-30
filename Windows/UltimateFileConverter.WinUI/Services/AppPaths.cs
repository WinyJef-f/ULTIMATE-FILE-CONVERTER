namespace UltimateFileConverter.WinUI.Services;

/// <summary>
/// Central location for the app's per-user data. Mirrors the macOS app's
/// <c>~/Library/Application Support/ULTIMATE-FILE-CONVERTER/</c> folder, here under
/// <c>%LOCALAPPDATA%\ULTIMATE-FILE-CONVERTER\</c>.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "ULTIMATE-FILE-CONVERTER";

    public static string DataFolder
    {
        get
        {
            var root = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            var folder = System.IO.Path.Combine(root, AppFolderName);
            System.IO.Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string SettingsFile => System.IO.Path.Combine(DataFolder, "settings.json");

    public static string HistoryFile => System.IO.Path.Combine(DataFolder, "history.json");

    /// <summary>Full history log used by the stats dashboard. Survives "Clear History".</summary>
    public static string FullHistoryFile => System.IO.Path.Combine(DataFolder, "history-full.json");

    /// <summary>Marker that records first-run dependency setup completed.</summary>
    public static string DependencyMarkerFile => System.IO.Path.Combine(DataFolder, "dependencies-v1.ok");

    /// <summary>Scratch directory for multi-step conversions (intermediate files).</summary>
    public static string TempFolder
    {
        get
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), AppFolderName);
            System.IO.Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
