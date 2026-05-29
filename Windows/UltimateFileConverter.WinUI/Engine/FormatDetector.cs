using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Engine;

/// <summary>
/// Determines a file's <see cref="FileKind"/> by extension first (fast, usually right),
/// falling back to a small magic-byte sniff for extension-less or mislabeled files —
/// the Windows analogue of the macOS <c>FormatDetector</c>'s UTType content sniffing.
/// </summary>
public static class FormatDetector
{
    public static FileKind? Detect(string path)
        => DetectByExtension(path) ?? DetectByContent(path);

    public static FileKind? DetectByExtension(string path)
    {
        var ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return null;
        foreach (var kind in Formats.All)
        {
            if (kind.RecognizedExtensions().Contains(ext)) return kind;
        }
        return null;
    }

    public static FileKind? DetectByContent(string path)
    {
        byte[] head;
        try
        {
            using var stream = System.IO.File.OpenRead(path);
            head = new byte[16];
            var read = stream.Read(head, 0, head.Length);
            if (read < head.Length) System.Array.Resize(ref head, read);
        }
        catch
        {
            return null;
        }

        if (StartsWith(head, 0x89, 0x50, 0x4E, 0x47)) return FileKind.Png;                       // ‰PNG
        if (StartsWith(head, 0xFF, 0xD8, 0xFF)) return FileKind.Jpeg;                             // JPEG SOI
        if (StartsWith(head, 0x47, 0x49, 0x46, 0x38)) return FileKind.Gif;                        // GIF8
        if (StartsWith(head, 0x42, 0x4D)) return FileKind.Bmp;                                    // BM
        if (StartsWith(head, 0x49, 0x49, 0x2A, 0x00) || StartsWith(head, 0x4D, 0x4D, 0x00, 0x2A)) // TIFF (LE/BE)
            return FileKind.Tiff;
        if (StartsWith(head, 0x25, 0x50, 0x44, 0x46)) return FileKind.Pdf;                        // %PDF
        if (StartsWith(head, 0x00, 0x00, 0x01, 0x00)) return FileKind.Ico;                        // ICO

        // RIFF container: distinguish WebP vs WAV/AVI by the form type at offset 8.
        if (StartsWith(head, 0x52, 0x49, 0x46, 0x46) && head.Length >= 12)
        {
            if (head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50) return FileKind.Webp; // WEBP
            if (head[8] == 0x57 && head[9] == 0x41 && head[10] == 0x56 && head[11] == 0x45) return FileKind.Wav;  // WAVE
            if (head[8] == 0x41 && head[9] == 0x56 && head[10] == 0x49 && head[11] == 0x20) return FileKind.Avi;  // AVI
        }

        // ISO-BMFF: ...ftyp at offset 4 covers MP4 / MOV / HEIC / AVIF / M4A.
        if (head.Length >= 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70)
        {
            var brand = System.Text.Encoding.ASCII.GetString(head, 8, 4);
            return brand switch
            {
                "heic" or "heix" or "mif1" => FileKind.Heic,
                "avif" => FileKind.Avif,
                "M4A " => FileKind.M4a,
                "qt  " => FileKind.Mov,
                _ => FileKind.Mp4,
            };
        }

        if (StartsWith(head, 0x4F, 0x67, 0x67, 0x53)) return FileKind.Ogg;   // OggS
        if (StartsWith(head, 0x66, 0x4C, 0x61, 0x43)) return FileKind.Flac;  // fLaC
        if (StartsWith(head, 0x1A, 0x45, 0xDF, 0xA3)) return FileKind.Mkv;   // EBML (Matroska/WebM)
        if (StartsWith(head, 0x49, 0x44, 0x33)) return FileKind.Mp3;         // ID3

        return null;
    }

    private static bool StartsWith(byte[] data, params byte[] prefix)
    {
        if (data.Length < prefix.Length) return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i]) return false;
        }
        return true;
    }
}
