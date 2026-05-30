namespace UltimateFileConverter.WinUI.Engine;

/// <summary>
/// The external command-line tools the router can dispatch to, plus the in-process
/// <see cref="Copy"/> sentinel used by Experimental mode. Windows analogue of the
/// macOS <c>Tool</c> enum — there is no <c>native</c> case because raster image, SVG,
/// and PDF rasterization are handled by ImageMagick (<see cref="Magick"/>) rather than
/// platform frameworks.
/// </summary>
public enum Tool
{
    Ffmpeg,
    Magick,
    Mutool,
    Pandoc,
    Soffice,
    Copy,
}

public static class ToolExtensions
{
    /// <summary>The executable filename to look for on disk.</summary>
    public static string ExecutableName(this Tool tool) => tool switch
    {
        Tool.Ffmpeg => "ffmpeg.exe",
        Tool.Magick => "magick.exe",
        Tool.Mutool => "mutool.exe",
        Tool.Pandoc => "pandoc.exe",
        Tool.Soffice => "soffice.exe",
        Tool.Copy => "<copy>",
        _ => throw new System.ArgumentOutOfRangeException(nameof(tool), tool, null),
    };

    /// <summary>Human-friendly tool name for status and error messages.</summary>
    public static string FriendlyName(this Tool tool) => tool switch
    {
        Tool.Ffmpeg => "FFmpeg",
        Tool.Magick => "ImageMagick",
        Tool.Mutool => "MuPDF",
        Tool.Pandoc => "Pandoc",
        Tool.Soffice => "LibreOffice",
        Tool.Copy => "file copy",
        _ => tool.ToString(),
    };

    /// <summary>winget package id used to install the tool on first run.</summary>
    public static string WingetPackageId(this Tool tool) => tool switch
    {
        Tool.Ffmpeg => "Gyan.FFmpeg",
        Tool.Magick => "ImageMagick.ImageMagick",
        Tool.Mutool => "ArtifexSoftware.mutool",
        Tool.Pandoc => "JohnMacFarlane.Pandoc",
        Tool.Soffice => "TheDocumentFoundation.LibreOffice",
        _ => string.Empty,
    };
}
