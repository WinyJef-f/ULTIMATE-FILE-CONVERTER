using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Engine;

public static class ConversionRouter
{
    private static readonly HashSet<FileKind> SofficeTextFamily = [FileKind.Docx, FileKind.Doc, FileKind.Odt, FileKind.Rtf, FileKind.Html, FileKind.Txt];
    private static readonly HashSet<FileKind> SofficeSheetFamily = [FileKind.Xlsx, FileKind.Ods, FileKind.Csv];
    private static readonly HashSet<FileKind> SofficePresFamily = [FileKind.Pptx, FileKind.Odp];

    public static ConversionPlan? Plan(FileKind source, FileKind target, string inputPath, string outputPath, ConversionSettings settings)
    {
        if (source == target) return null;

        var src = source.Category;
        var dst = target.Category;

        if (src == FileCategory.Image && dst == FileCategory.Image)
        {
            return Single(Tool.Magick, ["{INPUT}", "-quality", settings.ImageQuality.ToString(), "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Audio && dst == FileCategory.Audio)
        {
            return Single(Tool.Ffmpeg, ["-y", "-i", "{INPUT}", "-b:a", $"{settings.AudioBitrate}k", "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Video && dst == FileCategory.Video)
        {
            return Single(Tool.Ffmpeg, ["-y", "-i", "{INPUT}", "-crf", settings.VideoCRF.ToString(), "-preset", "medium", "-c:a", "aac", "-b:a", $"{settings.AudioBitrate}k", "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Video && dst == FileCategory.Audio)
        {
            return Single(Tool.Ffmpeg, ["-y", "-i", "{INPUT}", "-vn", "-b:a", $"{settings.AudioBitrate}k", "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Audio && dst == FileCategory.Video)
        {
            return Single(Tool.Ffmpeg, ["-y", "-f", "lavfi", "-i", "color=c=black:s=640x360", "-i", "{INPUT}", "-shortest", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", $"{settings.AudioBitrate}k", "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Image && dst == FileCategory.Video)
        {
            var args = source == FileKind.Gif
                ? new[] { "-y", "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}" }
                : ["-y", "-loop", "1", "-i", "{INPUT}", "-t", "5", "-pix_fmt", "yuv420p", "{OUTPUT}"];
            return Single(Tool.Ffmpeg, args, inputPath, outputPath);
        }

        if (src == FileCategory.Video && dst == FileCategory.Image)
        {
            var args = target == FileKind.Gif
                ? new[] { "-y", "-i", "{INPUT}", "-vf", "fps=12,scale=480:-1:flags=lanczos", "-loop", "0", "{OUTPUT}" }
                : ["-y", "-i", "{INPUT}", "-vframes", "1", "{OUTPUT}"];
            return Single(Tool.Ffmpeg, args, inputPath, outputPath);
        }

        if (source == FileKind.Pdf && dst == FileCategory.Image)
        {
            return Single(Tool.Magick, ["-density", "200", "{INPUT}[0]", "-quality", settings.ImageQuality.ToString(), "{OUTPUT}"], inputPath, outputPath);
        }

        if (src == FileCategory.Document && dst == FileCategory.Document && IsPandocPair(source, target))
        {
            return Single(Tool.Pandoc, ["{INPUT}", "-o", "{OUTPUT}"], inputPath, outputPath);
        }

        var soffice = SofficeRoute(source, target, inputPath, outputPath);
        if (soffice is not null) return soffice;

        return settings.WeirdModeEnabled ? WeirdRoute(target, inputPath, outputPath, settings) : null;
    }

    public static IReadOnlyList<FileKind> ValidTargets(FileKind source, ConversionSettings settings) =>
        FileKind.All.Where(target => target != source && Plan(source, target, $"_in.{source.CanonicalExtension}", $"_out.{target.CanonicalExtension}", settings) is not null).ToArray();

    private static ConversionPlan? SofficeRoute(FileKind source, FileKind target, string inputPath, string outputPath)
    {
        var readable = SofficeTextFamily.Concat(SofficeSheetFamily).Concat(SofficePresFamily).ToHashSet();
        if (target == FileKind.Pdf && readable.Contains(source)) return SofficePlan(target, inputPath, outputPath);
        if (SofficeTextFamily.Contains(source) && SofficeTextFamily.Contains(target)) return SofficePlan(target, inputPath, outputPath);
        if (SofficeSheetFamily.Contains(source) && SofficeSheetFamily.Contains(target)) return SofficePlan(target, inputPath, outputPath);
        if (SofficePresFamily.Contains(source) && SofficePresFamily.Contains(target)) return SofficePlan(target, inputPath, outputPath);
        return null;
    }

    private static ConversionPlan SofficePlan(FileKind target, string inputPath, string outputPath)
    {
        var profileDir = Path.Combine(Path.GetTempPath(), "ULTIMATE-FILE-CONVERTER-soffice").Replace("\\", "/");
        return Single(Tool.Soffice, [$"-env:UserInstallation=file:///{profileDir}", "--headless", "--convert-to", target.CanonicalExtension, "--outdir", "{OUTPUT_DIR}", "{INPUT}"], inputPath, outputPath);
    }

    private static bool IsPandocPair(FileKind source, FileKind target)
    {
        HashSet<FileKind> pandocKinds = [FileKind.Md, FileKind.Html, FileKind.Docx, FileKind.Odt, FileKind.Rtf, FileKind.Epub, FileKind.Txt, FileKind.Tex];
        return pandocKinds.Contains(source) && pandocKinds.Contains(target);
    }

    private static ConversionPlan WeirdRoute(FileKind target, string inputPath, string outputPath, ConversionSettings settings) => target.Category switch
    {
        FileCategory.Image => RawBytesToImage(target, inputPath, outputPath, settings),
        FileCategory.Audio => Single(Tool.Ffmpeg, ["-y", "-f", "u8", "-ar", "22050", "-ac", "1", "-i", "{INPUT}", "{OUTPUT}"], inputPath, outputPath),
        FileCategory.Video => Single(Tool.Ffmpeg, ["-y", "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "256x256", "-framerate", "10", "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}"], inputPath, outputPath),
        _ => Single(Tool.Copy, ["{INPUT}", "{OUTPUT}"], inputPath, outputPath)
    };

    private static ConversionPlan RawBytesToImage(FileKind target, string inputPath, string outputPath, ConversionSettings settings)
    {
        var pngArgs = new[] { "-y", "-f", "rawvideo", "-pixel_format", "gray", "-video_size", "256x256", "-i", "{INPUT}", "-frames:v", "1", "{OUTPUT}" };
        if (target == FileKind.Png) return Single(Tool.Ffmpeg, pngArgs, inputPath, outputPath);

        var intermediate = Path.Combine(Path.GetTempPath(), "ULTIMATE-FILE-CONVERTER", $"{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(intermediate)!);
        return new ConversionPlan(
            new ConversionStep(Tool.Ffmpeg, pngArgs, inputPath, intermediate),
            new ConversionStep(Tool.Magick, ["{INPUT}", "-quality", settings.ImageQuality.ToString(), "{OUTPUT}"], intermediate, outputPath));
    }

    private static ConversionPlan Single(Tool tool, IReadOnlyList<string> arguments, string inputPath, string outputPath) =>
        new(new ConversionStep(tool, arguments, inputPath, outputPath));
}
