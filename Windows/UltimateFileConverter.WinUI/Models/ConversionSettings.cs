using System.Text.Json.Serialization;

namespace UltimateFileConverter.WinUI.Models;

public enum OutputFolderMode
{
    NextToSource,
    CustomFolder,
}

public static class OutputFolderModeExtensions
{
    public static string DisplayName(this OutputFolderMode mode) => mode switch
    {
        OutputFolderMode.NextToSource => "Next to source file",
        OutputFolderMode.CustomFolder => "Custom folder…",
        _ => mode.ToString(),
    };
}

/// <summary>
/// User-tunable conversion settings. Persisted as JSON in the app's LocalAppData folder,
/// mirroring the macOS app's UserDefaults-backed <c>ConversionSettings</c>.
/// </summary>
public sealed class ConversionSettings
{
    /// <summary>Image quality 1-100. Applied via <c>-quality</c> to ImageMagick.</summary>
    public int ImageQuality { get; set; } = 85;

    /// <summary>Audio bitrate in kbps. Applied via <c>-b:a Nk</c> to ffmpeg.</summary>
    public int AudioBitrate { get; set; } = 192;

    /// <summary>Video constant rate factor (lower = better, larger files). Applied via <c>-crf</c> to ffmpeg.</summary>
    public int VideoCRF { get; set; } = 23;

    /// <summary>Where converted files are written.</summary>
    public OutputFolderMode OutputFolderMode { get; set; } = OutputFolderMode.NextToSource;

    /// <summary>Absolute path of the custom output folder (when <see cref="OutputFolderMode"/> == CustomFolder).</summary>
    public string? CustomOutputFolderPath { get; set; }

    /// <summary>
    /// When true, the router will route ANY source -> ANY target using raw-byte
    /// reinterpretation and file-copy fallbacks. Results vary wildly.
    /// </summary>
    public bool WeirdModeEnabled { get; set; }

    public ConversionSettings Clone() => new()
    {
        ImageQuality = ImageQuality,
        AudioBitrate = AudioBitrate,
        VideoCRF = VideoCRF,
        OutputFolderMode = OutputFolderMode,
        CustomOutputFolderPath = CustomOutputFolderPath,
        WeirdModeEnabled = WeirdModeEnabled,
    };

    [JsonIgnore]
    public bool HasUsableCustomFolder =>
        OutputFolderMode == OutputFolderMode.CustomFolder &&
        !string.IsNullOrWhiteSpace(CustomOutputFolderPath);
}
