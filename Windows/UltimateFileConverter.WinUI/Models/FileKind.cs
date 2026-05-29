namespace UltimateFileConverter.WinUI.Models;

public enum FileCategory
{
    Image,
    Audio,
    Video,
    Document,
    Spreadsheet,
    Presentation
}

public sealed record FileKind(string Id, string DisplayName, FileCategory Category, string CanonicalExtension, IReadOnlyList<string> Extensions)
{
    public static readonly FileKind Jpeg = new("jpeg", "JPEG", FileCategory.Image, "jpg", ["jpg", "jpeg", "jpe", "jfif"]);
    public static readonly FileKind Png = new("png", "PNG", FileCategory.Image, "png", ["png"]);
    public static readonly FileKind Webp = new("webp", "WebP", FileCategory.Image, "webp", ["webp"]);
    public static readonly FileKind Heic = new("heic", "HEIC", FileCategory.Image, "heic", ["heic", "heif"]);
    public static readonly FileKind Avif = new("avif", "AVIF", FileCategory.Image, "avif", ["avif"]);
    public static readonly FileKind Gif = new("gif", "GIF", FileCategory.Image, "gif", ["gif"]);
    public static readonly FileKind Bmp = new("bmp", "BMP", FileCategory.Image, "bmp", ["bmp"]);
    public static readonly FileKind Tiff = new("tiff", "TIFF", FileCategory.Image, "tif", ["tif", "tiff"]);
    public static readonly FileKind Svg = new("svg", "SVG", FileCategory.Image, "svg", ["svg"]);
    public static readonly FileKind Ico = new("ico", "ICO", FileCategory.Image, "ico", ["ico"]);
    public static readonly FileKind Mp3 = new("mp3", "MP3", FileCategory.Audio, "mp3", ["mp3"]);
    public static readonly FileKind Wav = new("wav", "WAV", FileCategory.Audio, "wav", ["wav", "wave"]);
    public static readonly FileKind Flac = new("flac", "FLAC", FileCategory.Audio, "flac", ["flac"]);
    public static readonly FileKind Aac = new("aac", "AAC", FileCategory.Audio, "aac", ["aac"]);
    public static readonly FileKind M4a = new("m4a", "M4A", FileCategory.Audio, "m4a", ["m4a"]);
    public static readonly FileKind Ogg = new("ogg", "OGG", FileCategory.Audio, "ogg", ["ogg", "oga"]);
    public static readonly FileKind Opus = new("opus", "Opus", FileCategory.Audio, "opus", ["opus"]);
    public static readonly FileKind Aiff = new("aiff", "AIFF", FileCategory.Audio, "aiff", ["aiff", "aif", "aifc"]);
    public static readonly FileKind Mp4 = new("mp4", "MP4", FileCategory.Video, "mp4", ["mp4", "m4v"]);
    public static readonly FileKind Mov = new("mov", "MOV", FileCategory.Video, "mov", ["mov"]);
    public static readonly FileKind Mkv = new("mkv", "MKV", FileCategory.Video, "mkv", ["mkv"]);
    public static readonly FileKind Webm = new("webm", "WebM", FileCategory.Video, "webm", ["webm"]);
    public static readonly FileKind Avi = new("avi", "AVI", FileCategory.Video, "avi", ["avi"]);
    public static readonly FileKind Pdf = new("pdf", "PDF", FileCategory.Document, "pdf", ["pdf"]);
    public static readonly FileKind Docx = new("docx", "Word (.docx)", FileCategory.Document, "docx", ["docx"]);
    public static readonly FileKind Doc = new("doc", "Word (.doc)", FileCategory.Document, "doc", ["doc"]);
    public static readonly FileKind Odt = new("odt", "OpenDocument Text", FileCategory.Document, "odt", ["odt"]);
    public static readonly FileKind Rtf = new("rtf", "Rich Text", FileCategory.Document, "rtf", ["rtf"]);
    public static readonly FileKind Html = new("html", "HTML", FileCategory.Document, "html", ["html", "htm"]);
    public static readonly FileKind Md = new("md", "Markdown", FileCategory.Document, "md", ["md", "markdown", "mdown"]);
    public static readonly FileKind Epub = new("epub", "EPUB", FileCategory.Document, "epub", ["epub"]);
    public static readonly FileKind Txt = new("txt", "Plain Text", FileCategory.Document, "txt", ["txt", "text"]);
    public static readonly FileKind Tex = new("tex", "LaTeX", FileCategory.Document, "tex", ["tex", "latex"]);
    public static readonly FileKind Xlsx = new("xlsx", "Excel (.xlsx)", FileCategory.Spreadsheet, "xlsx", ["xlsx"]);
    public static readonly FileKind Ods = new("ods", "OpenDocument Sheet", FileCategory.Spreadsheet, "ods", ["ods"]);
    public static readonly FileKind Csv = new("csv", "CSV", FileCategory.Spreadsheet, "csv", ["csv"]);
    public static readonly FileKind Pptx = new("pptx", "PowerPoint (.pptx)", FileCategory.Presentation, "pptx", ["pptx"]);
    public static readonly FileKind Odp = new("odp", "OpenDocument Pres.", FileCategory.Presentation, "odp", ["odp"]);

    public static IReadOnlyList<FileKind> All { get; } =
    [
        Jpeg, Png, Webp, Heic, Avif, Gif, Bmp, Tiff, Svg, Ico,
        Mp3, Wav, Flac, Aac, M4a, Ogg, Opus, Aiff,
        Mp4, Mov, Mkv, Webm, Avi,
        Pdf, Docx, Doc, Odt, Rtf, Html, Md, Epub, Txt, Tex,
        Xlsx, Ods, Csv,
        Pptx, Odp
    ];

    public static FileKind? FromPath(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return All.FirstOrDefault(kind => kind.Extensions.Contains(ext));
    }

    public override string ToString() => DisplayName;
}
