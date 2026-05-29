using System.Text.Json.Serialization;
using UltimateFileConverter.WinUI.Services;

namespace UltimateFileConverter.WinUI.Models;

/// <summary>
/// A persisted record of a past conversion. Port of the macOS <c>HistoryEntry</c>.
/// Format kinds are stored by their stable raw value and dates as ISO-8601, so the
/// on-disk shape matches the macOS <c>history.json</c>.
/// </summary>
public sealed class HistoryEntry
{
    public System.Guid Id { get; set; } = System.Guid.NewGuid();
    public System.DateTimeOffset Date { get; set; } = System.DateTimeOffset.Now;

    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Stable raw value of the source <see cref="FileKind"/> (e.g. "jpeg").</summary>
    public string SourceKind { get; set; } = string.Empty;

    public string? OutputPath { get; set; }

    /// <summary>Stable raw value of the output <see cref="FileKind"/>.</summary>
    public string OutputKind { get; set; } = string.Empty;

    /// <summary>"success" or "failure".</summary>
    public string Outcome { get; set; } = "failure";

    public string? ErrorMessage { get; set; }

    public static HistoryEntry Create(
        string sourcePath,
        FileKind sourceKind,
        string? outputPath,
        FileKind outputKind,
        bool success,
        string? errorMessage)
    {
        return new HistoryEntry
        {
            Id = System.Guid.NewGuid(),
            Date = System.DateTimeOffset.Now,
            SourcePath = sourcePath,
            SourceKind = sourceKind.RawValue(),
            OutputPath = success ? outputPath : null,
            OutputKind = outputKind.RawValue(),
            Outcome = success ? "success" : "failure",
            ErrorMessage = success ? null : errorMessage,
        };
    }

    // MARK: - Derived presentation properties (not serialized)

    [JsonIgnore]
    public bool IsSuccess => Outcome == "success";

    [JsonIgnore]
    public string SourceName => string.IsNullOrEmpty(SourcePath) ? "" : System.IO.Path.GetFileName(SourcePath);

    [JsonIgnore]
    public FileKind? SourceKindEnum => Formats.FromRawValue(SourceKind);

    [JsonIgnore]
    public FileKind? OutputKindEnum => Formats.FromRawValue(OutputKind);

    [JsonIgnore]
    public string SourceDisplay => SourceKindEnum?.DisplayName() ?? SourceKind.ToUpperInvariant();

    [JsonIgnore]
    public string OutputDisplay => OutputKindEnum?.DisplayName() ?? OutputKind.ToUpperInvariant();

    [JsonIgnore]
    public string CategoryGlyph => (SourceKindEnum?.Category() ?? FileCategory.Document).Glyph();

    /// <summary>"JPEG → PNG · 3m" — matches the macOS history metadata line.</summary>
    [JsonIgnore]
    public string MetadataLine => $"{SourceDisplay}  →  {OutputDisplay}   ·   {RelativeTime.Format(Date)}";

    [JsonIgnore]
    public bool OutputExists => IsSuccess && !string.IsNullOrEmpty(OutputPath) && System.IO.File.Exists(OutputPath);

    /// <summary>Show the "reveal" button only when a successful output is still on disk.</summary>
    [JsonIgnore]
    public bool ShowRevealButton => OutputExists;

    /// <summary>A successful entry whose output file is gone (moved/deleted).</summary>
    [JsonIgnore]
    public bool ShowMissingGlyph => IsSuccess && !OutputExists;

    [JsonIgnore]
    public bool ShowFailureGlyph => !IsSuccess;

    [JsonIgnore]
    public string? FailureTooltip => ErrorMessage ?? "Conversion failed";
}
