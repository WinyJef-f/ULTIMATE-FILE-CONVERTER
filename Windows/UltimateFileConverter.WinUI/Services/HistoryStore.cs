using System.Text.Json;
using System.Text.Json.Serialization;
using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Services;

/// <summary>
/// Loads and saves conversion history as JSON in the app data folder, mirroring the
/// macOS <c>history.json</c> (newest-first, capped to a fixed number of entries).
/// </summary>
public static class HistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<HistoryEntry> Load()
    {
        try
        {
            var path = AppPaths.HistoryFile;
            if (!System.IO.File.Exists(path)) return new List<HistoryEntry>();
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options) ?? new List<HistoryEntry>();
        }
        catch
        {
            return new List<HistoryEntry>();
        }
    }

    public static void Save(IEnumerable<HistoryEntry> history)
    {
        try
        {
            var json = JsonSerializer.Serialize(history.ToList(), Options);
            System.IO.File.WriteAllText(AppPaths.HistoryFile, json);
        }
        catch
        {
            // Best-effort; history is non-critical.
        }
    }

    public static List<HistoryEntry> LoadFull()
    {
        try
        {
            var path = AppPaths.FullHistoryFile;
            if (!System.IO.File.Exists(path)) return new List<HistoryEntry>();
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options) ?? new List<HistoryEntry>();
        }
        catch
        {
            return new List<HistoryEntry>();
        }
    }

    public static void SaveFull(IEnumerable<HistoryEntry> history)
    {
        try
        {
            var json = JsonSerializer.Serialize(history.ToList(), Options);
            System.IO.File.WriteAllText(AppPaths.FullHistoryFile, json);
        }
        catch
        {
            // Best-effort; history is non-critical.
        }
    }
}
