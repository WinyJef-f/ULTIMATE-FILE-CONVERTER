namespace UltimateFileConverter.WinUI.Models;

/// <summary>Broad family a <see cref="FileKind"/> belongs to. Mirrors the macOS FileCategory.</summary>
public enum FileCategory
{
    Image,
    Audio,
    Video,
    Document,
    Spreadsheet,
    Presentation,
    Subtitle,
    Archive,
    Font,
}

/// <summary>
/// Every concrete format the app understands. The raw name (lower-cased enum name)
/// is the stable identifier used in persisted history and in conversion-plan arguments,
/// exactly like the macOS <c>FileKind</c> rawValue.
/// </summary>
public enum FileKind
{
    // Image
    Jpeg, Png, Webp, Heic, Avif, Gif, Bmp, Tiff, Svg, Ico,
    // Audio
    Mp3, Wav, Flac, Aac, M4a, Ogg, Opus, Aiff,
    // Video
    Mp4, Mov, Mkv, Webm, Avi,
    // Document
    Pdf, Docx, Doc, Odt, Rtf, Html, Md, Epub, Txt, Tex, Azw3, Mobi,
    // Spreadsheet
    Xlsx, Ods, Csv,
    // Presentation
    Pptx, Odp,
    // Subtitle
    Srt, Ass, Vtt, Sbv,
    // Archive
    Zip, SevenZ, Tar, TarGz,
    // Font
    Ttf, Otf, Woff, Woff2,
    // RAW Photo (source-only — never a conversion target)
    Cr2, Nef, Arw, Dng,
}

/// <summary>
/// Metadata and helpers for <see cref="FileKind"/> / <see cref="FileCategory"/>.
/// The category mapping, display names, recognized extensions, and canonical
/// extension are a 1:1 port of the macOS model so both platforms agree on routing.
/// </summary>
public static class Formats
{
    public static readonly IReadOnlyList<FileKind> All = (FileKind[])System.Enum.GetValues(typeof(FileKind));

    public static readonly IReadOnlyList<FileCategory> AllCategories =
        (FileCategory[])System.Enum.GetValues(typeof(FileCategory));

    public static FileCategory Category(this FileKind kind) => kind switch
    {
        FileKind.Jpeg or FileKind.Png or FileKind.Webp or FileKind.Heic or FileKind.Avif
            or FileKind.Gif or FileKind.Bmp or FileKind.Tiff or FileKind.Svg or FileKind.Ico => FileCategory.Image,
        FileKind.Mp3 or FileKind.Wav or FileKind.Flac or FileKind.Aac or FileKind.M4a
            or FileKind.Ogg or FileKind.Opus or FileKind.Aiff => FileCategory.Audio,
        FileKind.Mp4 or FileKind.Mov or FileKind.Mkv or FileKind.Webm or FileKind.Avi => FileCategory.Video,
        FileKind.Pdf or FileKind.Docx or FileKind.Doc or FileKind.Odt or FileKind.Rtf
            or FileKind.Html or FileKind.Md or FileKind.Epub or FileKind.Txt or FileKind.Tex
            or FileKind.Azw3 or FileKind.Mobi => FileCategory.Document,
        FileKind.Xlsx or FileKind.Ods or FileKind.Csv => FileCategory.Spreadsheet,
        FileKind.Pptx or FileKind.Odp => FileCategory.Presentation,
        FileKind.Srt or FileKind.Ass or FileKind.Vtt or FileKind.Sbv => FileCategory.Subtitle,
        FileKind.Zip or FileKind.SevenZ or FileKind.Tar or FileKind.TarGz => FileCategory.Archive,
        FileKind.Ttf or FileKind.Otf or FileKind.Woff or FileKind.Woff2 => FileCategory.Font,
        FileKind.Cr2 or FileKind.Nef or FileKind.Arw or FileKind.Dng => FileCategory.Image,
        _ => FileCategory.Document,
    };

    /// <summary>True for RAW camera formats that are source-only — they are never conversion targets.</summary>
    public static bool IsSourceOnly(this FileKind kind) =>
        kind is FileKind.Cr2 or FileKind.Nef or FileKind.Arw or FileKind.Dng;

    public static string DisplayName(this FileKind kind) => kind switch
    {
        FileKind.Jpeg => "JPEG",
        FileKind.Png => "PNG",
        FileKind.Webp => "WebP",
        FileKind.Heic => "HEIC",
        FileKind.Gif => "GIF",
        FileKind.Bmp => "BMP",
        FileKind.Tiff => "TIFF",
        FileKind.Avif => "AVIF",
        FileKind.Svg => "SVG",
        FileKind.Ico => "ICO",
        FileKind.Mp3 => "MP3",
        FileKind.Wav => "WAV",
        FileKind.Flac => "FLAC",
        FileKind.Aac => "AAC",
        FileKind.M4a => "M4A",
        FileKind.Ogg => "OGG",
        FileKind.Opus => "Opus",
        FileKind.Aiff => "AIFF",
        FileKind.Mp4 => "MP4",
        FileKind.Mov => "MOV",
        FileKind.Mkv => "MKV",
        FileKind.Webm => "WebM",
        FileKind.Avi => "AVI",
        FileKind.Pdf => "PDF",
        FileKind.Docx => "Word (.docx)",
        FileKind.Doc => "Word (.doc)",
        FileKind.Odt => "OpenDocument Text",
        FileKind.Rtf => "Rich Text",
        FileKind.Html => "HTML",
        FileKind.Md => "Markdown",
        FileKind.Epub => "EPUB",
        FileKind.Txt => "Plain Text",
        FileKind.Tex => "LaTeX",
        FileKind.Azw3 => "Kindle (AZW3)",
        FileKind.Mobi => "Mobipocket (MOBI)",
        FileKind.Xlsx => "Excel (.xlsx)",
        FileKind.Ods => "OpenDocument Sheet",
        FileKind.Csv => "CSV",
        FileKind.Pptx => "PowerPoint (.pptx)",
        FileKind.Odp => "OpenDocument Pres.",
        FileKind.Srt => "SubRip",
        FileKind.Ass => "SSA/ASS",
        FileKind.Vtt => "WebVTT",
        FileKind.Sbv => "YouTube SBV",
        FileKind.Zip => "ZIP",
        FileKind.SevenZ => "7-Zip",
        FileKind.Tar => "TAR",
        FileKind.TarGz => "TAR.GZ",
        FileKind.Ttf => "TrueType (TTF)",
        FileKind.Otf => "OpenType (OTF)",
        FileKind.Woff => "WOFF",
        FileKind.Woff2 => "WOFF2",
        FileKind.Cr2 => "Canon RAW (CR2)",
        FileKind.Nef => "Nikon RAW (NEF)",
        FileKind.Arw => "Sony RAW (ARW)",
        FileKind.Dng => "DNG",
        _ => kind.ToString().ToUpperInvariant(),
    };

    /// <summary>Every file extension (lower-cased, no dot) that maps to this kind.</summary>
    public static IReadOnlyList<string> RecognizedExtensions(this FileKind kind) => kind switch
    {
        FileKind.Jpeg => new[] { "jpg", "jpeg", "jpe", "jfif" },
        FileKind.Png => new[] { "png" },
        FileKind.Webp => new[] { "webp" },
        FileKind.Heic => new[] { "heic", "heif" },
        FileKind.Gif => new[] { "gif" },
        FileKind.Bmp => new[] { "bmp" },
        FileKind.Tiff => new[] { "tif", "tiff" },
        FileKind.Avif => new[] { "avif" },
        FileKind.Svg => new[] { "svg" },
        FileKind.Ico => new[] { "ico" },
        FileKind.Mp3 => new[] { "mp3" },
        FileKind.Wav => new[] { "wav", "wave" },
        FileKind.Flac => new[] { "flac" },
        FileKind.Aac => new[] { "aac" },
        FileKind.M4a => new[] { "m4a" },
        FileKind.Ogg => new[] { "ogg", "oga" },
        FileKind.Opus => new[] { "opus" },
        FileKind.Aiff => new[] { "aiff", "aif", "aifc" },
        FileKind.Mp4 => new[] { "mp4", "m4v" },
        FileKind.Mov => new[] { "mov" },
        FileKind.Mkv => new[] { "mkv" },
        FileKind.Webm => new[] { "webm" },
        FileKind.Avi => new[] { "avi" },
        FileKind.Pdf => new[] { "pdf" },
        FileKind.Docx => new[] { "docx" },
        FileKind.Doc => new[] { "doc" },
        FileKind.Odt => new[] { "odt" },
        FileKind.Rtf => new[] { "rtf" },
        FileKind.Html => new[] { "html", "htm" },
        FileKind.Md => new[] { "md", "markdown", "mdown" },
        FileKind.Epub => new[] { "epub" },
        FileKind.Txt => new[] { "txt", "text" },
        FileKind.Tex => new[] { "tex", "latex" },
        FileKind.Azw3 => new[] { "azw3" },
        FileKind.Mobi => new[] { "mobi" },
        FileKind.Xlsx => new[] { "xlsx" },
        FileKind.Ods => new[] { "ods" },
        FileKind.Csv => new[] { "csv" },
        FileKind.Pptx => new[] { "pptx" },
        FileKind.Odp => new[] { "odp" },
        FileKind.Srt => new[] { "srt" },
        FileKind.Ass => new[] { "ass", "ssa" },
        FileKind.Vtt => new[] { "vtt" },
        FileKind.Sbv => new[] { "sbv" },
        FileKind.Zip => new[] { "zip" },
        FileKind.SevenZ => new[] { "7z" },
        FileKind.Tar => new[] { "tar" },
        FileKind.TarGz => new[] { "tgz", "tar.gz" },
        FileKind.Ttf => new[] { "ttf" },
        FileKind.Otf => new[] { "otf" },
        FileKind.Woff => new[] { "woff" },
        FileKind.Woff2 => new[] { "woff2" },
        FileKind.Cr2 => new[] { "cr2" },
        FileKind.Nef => new[] { "nef" },
        FileKind.Arw => new[] { "arw" },
        FileKind.Dng => new[] { "dng" },
        _ => new[] { kind.ToString().ToLowerInvariant() },
    };

    /// <summary>Extension used when writing an output file.</summary>
    public static string CanonicalExtension(this FileKind kind) =>
        // TarGz uses "tar.gz" (compound) as its canonical output extension rather than "tgz".
        kind == FileKind.TarGz ? "tar.gz" : kind.RecognizedExtensions()[0];

    /// <summary>Stable lower-case identifier (matches the macOS rawValue) used in plans and history.</summary>
    public static string RawValue(this FileKind kind) => kind switch
    {
        // These don't follow the plain ToString().ToLower() pattern.
        FileKind.SevenZ => "sevenz",
        FileKind.TarGz => "targz",
        _ => kind.ToString().ToLowerInvariant(),
    };

    public static FileKind? FromRawValue(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        foreach (var k in All)
        {
            if (k.RawValue() == raw) return k;
        }
        return null;
    }

    public static string DisplayName(this FileCategory category) => category switch
    {
        FileCategory.Image => "Image",
        FileCategory.Audio => "Audio",
        FileCategory.Video => "Video",
        FileCategory.Document => "Document",
        FileCategory.Spreadsheet => "Spreadsheet",
        FileCategory.Presentation => "Presentation",
        FileCategory.Subtitle => "Subtitle",
        FileCategory.Archive => "Archive",
        FileCategory.Font => "Font",
        _ => category.ToString(),
    };

    /// <summary>Segoe Fluent / MDL2 glyph code used as the per-category badge.</summary>
    public static string Glyph(this FileCategory category) => category switch
    {
        FileCategory.Image => "\uE91B",        // Photo
        FileCategory.Audio => "\uEC4F",        // MusicNote
        FileCategory.Video => "\uE714",        // Video
        FileCategory.Document => "\uE8A5",     // Document
        FileCategory.Spreadsheet => "\uE8A5",  // Document (shared)
        FileCategory.Presentation => "\uE786", // Slideshow
        FileCategory.Subtitle => "\uE7F0",     // ClosedCaption
        FileCategory.Archive => "\uF5ED",      // ZipFolder
        FileCategory.Font => "\uE8D2",         // Font
        _ => "\uE8A5",
    };
}
