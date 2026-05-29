namespace UltimateFileConverter.WinUI.Engine;

public enum Tool
{
    Ffmpeg,
    Magick,
    Pandoc,
    Soffice,
    SevenZip,
    Copy
}

public static class ToolExtensions
{
    public static string ExecutableName(this Tool tool) => tool switch
    {
        Tool.Ffmpeg => "ffmpeg.exe",
        Tool.Magick => "magick.exe",
        Tool.Pandoc => "pandoc.exe",
        Tool.Soffice => "soffice.exe",
        Tool.SevenZip => "7z.exe",
        Tool.Copy => "copy",
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
    };

    public static string WingetPackageId(this Tool tool) => tool switch
    {
        Tool.Ffmpeg => "Gyan.FFmpeg",
        Tool.Magick => "ImageMagick.ImageMagick",
        Tool.Pandoc => "JohnMacFarlane.Pandoc",
        Tool.Soffice => "TheDocumentFoundation.LibreOffice",
        Tool.SevenZip => "7zip.7zip",
        Tool.Copy => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
    };
}
