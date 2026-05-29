namespace UltimateFileConverter.WinUI.Models;

public enum OutputFolderMode
{
    NextToSource,
    CustomFolder
}

public sealed class ConversionSettings
{
    public int ImageQuality { get; set; } = 85;
    public int AudioBitrate { get; set; } = 192;
    public int VideoCRF { get; set; } = 23;
    public OutputFolderMode OutputFolderMode { get; set; } = OutputFolderMode.NextToSource;
    public string? CustomOutputFolderPath { get; set; }
    public bool WeirdModeEnabled { get; set; }
}
