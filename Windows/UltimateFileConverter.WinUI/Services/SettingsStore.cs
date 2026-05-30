using System.Text.Json;
using System.Text.Json.Serialization;
using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Services;

/// <summary>Loads and saves <see cref="ConversionSettings"/> as JSON, like the macOS UserDefaults persistence.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ConversionSettings Load()
    {
        try
        {
            var path = AppPaths.SettingsFile;
            if (!System.IO.File.Exists(path)) return new ConversionSettings();
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConversionSettings>(json, Options) ?? new ConversionSettings();
        }
        catch
        {
            return new ConversionSettings();
        }
    }

    public static void Save(ConversionSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, Options);
            System.IO.File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch
        {
            // Persisting settings is best-effort; never crash the app over it.
        }
    }
}
