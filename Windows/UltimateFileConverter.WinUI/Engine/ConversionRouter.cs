using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.Engine;

/// <summary>
/// Maps a (source, target, settings) triple to a <see cref="ConversionPlan"/>. Faithful
/// port of the macOS <c>ConversionRouter</c>; the only platform difference is that raster
/// image, SVG, and PDF rasterization use ImageMagick (the macOS app uses native CGImage /
/// NSImage / CGPDFDocument). Branch ordering matches macOS so both platforms agree on which
/// targets are valid for a given source.
/// </summary>
public static class ConversionRouter
{
    private static readonly HashSet<FileKind> SofficeTextFamily =
        new() { FileKind.Docx, FileKind.Doc, FileKind.Odt, FileKind.Rtf, FileKind.Html, FileKind.Txt };

    private static readonly HashSet<FileKind> SofficeSheetFamily =
        new() { FileKind.Xlsx, FileKind.Ods, FileKind.Csv };

    private static readonly HashSet<FileKind> SofficePresFamily =
        new() { FileKind.Pptx, FileKind.Odp };

    private static readonly HashSet<FileKind> PandocKinds =
        new() { FileKind.Md, FileKind.Html, FileKind.Docx, FileKind.Odt, FileKind.Rtf, FileKind.Epub, FileKind.Txt, FileKind.Tex };

    private static HashSet<FileKind> SofficeReadable
    {
        get
        {
            var set = new HashSet<FileKind>(SofficeTextFamily);
            set.UnionWith(SofficeSheetFamily);
            set.UnionWith(SofficePresFamily);
            return set;
        }
    }

    public static ConversionPlan? Plan(
        FileKind source, FileKind target, string inputPath, string outputPath, ConversionSettings settings)
    {
        if (source == target) return null;

        var src = source.Category();
        var dst = target.Category();
        var q = settings.ImageQuality.ToString();
        var br = settings.AudioBitrate.ToString();

        // --- SVG source: rasterize at high density via ImageMagick for crispness ---
        if (source == FileKind.Svg && dst == FileCategory.Image)
        {
            return ConversionPlan.Single(Tool.Magick,
                new[] { "-density", "384", "-background", "none", "{INPUT}", "-quality", q, "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Image -> Image via ImageMagick ([0] selects the first frame, matching macOS) ---
        if (src == FileCategory.Image && dst == FileCategory.Image)
        {
            return ConversionPlan.Single(Tool.Magick,
                new[] { "{INPUT}[0]", "-quality", q, "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Audio -> Audio via ffmpeg ---
        if (src == FileCategory.Audio && dst == FileCategory.Audio)
        {
            return ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-i", "{INPUT}", "-b:a", $"{br}k", "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Video -> Video via ffmpeg ---
        if (src == FileCategory.Video && dst == FileCategory.Video)
        {
            return ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-i", "{INPUT}", "-crf", settings.VideoCRF.ToString(), "-preset", "medium",
                        "-c:a", "aac", "-b:a", $"{br}k", "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Video -> Audio: drop the video stream ---
        if (src == FileCategory.Video && dst == FileCategory.Audio)
        {
            return ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-i", "{INPUT}", "-vn", "-b:a", $"{br}k", "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Audio -> Video: pair audio with a black still image ---
        if (src == FileCategory.Audio && dst == FileCategory.Video)
        {
            return ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-f", "lavfi", "-i", "color=c=black:s=640x360", "-i", "{INPUT}",
                        "-shortest", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", $"{br}k", "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- Image -> Video ---
        if (src == FileCategory.Image && dst == FileCategory.Video)
        {
            var args = source == FileKind.Gif
                ? new[] { "-y", "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}" }                       // preserve GIF frames
                : new[] { "-y", "-loop", "1", "-i", "{INPUT}", "-t", "5", "-pix_fmt", "yuv420p", "{OUTPUT}" }; // loop a still 5s
            return ConversionPlan.Single(Tool.Ffmpeg, args, inputPath, outputPath);
        }

        // --- Video -> Image ---
        if (src == FileCategory.Video && dst == FileCategory.Image)
        {
            var args = target == FileKind.Gif
                ? new[] { "-y", "-i", "{INPUT}", "-vf", "fps=12,scale=480:-1:flags=lanczos", "-loop", "0", "{OUTPUT}" }
                : new[] { "-y", "-i", "{INPUT}", "-vframes", "1", "{OUTPUT}" };
            return ConversionPlan.Single(Tool.Ffmpeg, args, inputPath, outputPath);
        }

        // --- PDF -> Image: rasterize the first page via mutool draw (no Ghostscript needed) ---
        if (source == FileKind.Pdf && dst == FileCategory.Image)
        {
            // mutool draw picks the output format from the file extension.
            // PNG, JPEG, and BMP are supported natively; all other image targets go through
            // a temp PNG and then ImageMagick to reach formats mutool can't write directly.
            if (target is FileKind.Png or FileKind.Jpeg or FileKind.Bmp)
            {
                return ConversionPlan.Single(Tool.Mutool,
                    new[] { "draw", "-o", "{OUTPUT}", "-r", "200", "{INPUT}", "1" },
                    inputPath, outputPath);
            }
            var tmp = System.IO.Path.Combine(AppPathsTemp(), $"{System.Guid.NewGuid():N}.png");
            return new ConversionPlan(
                new ConversionStep(Tool.Mutool,
                    new[] { "draw", "-o", "{OUTPUT}", "-r", "200", "{INPUT}", "1" },
                    inputPath, tmp),
                new ConversionStep(Tool.Magick,
                    new[] { "{INPUT}", "-quality", q, "{OUTPUT}" },
                    tmp, outputPath));
        }

        // --- Document -> Document via pandoc ---
        if (src == FileCategory.Document && dst == FileCategory.Document && IsPandocPair(source, target))
        {
            return ConversionPlan.Single(Tool.Pandoc,
                new[] { "{INPUT}", "-o", "{OUTPUT}" },
                inputPath, outputPath);
        }

        // --- LibreOffice (soffice) family conversions ---
        var soffice = SofficeRoute(source, target, inputPath, outputPath);
        if (soffice is not null) return soffice;

        // --- Experimental mode: any -> any via raw-byte reinterpretation / file copy ---
        if (settings.WeirdModeEnabled)
        {
            return WeirdRoute(target, inputPath, outputPath, settings);
        }

        return null;
    }

    public static bool CanConvert(FileKind source, FileKind target, ConversionSettings settings)
    {
        var probeIn = $"_in.{source.CanonicalExtension()}";
        var probeOut = $"_out.{target.CanonicalExtension()}";
        return Plan(source, target, probeIn, probeOut, settings) is not null;
    }

    /// <summary>
    /// All targets reachable from <paramref name="source"/>, sorted by category then display
    /// name — exactly matching the macOS ordering (used to choose the default target).
    /// </summary>
    public static IReadOnlyList<FileKind> ValidTargets(FileKind source, ConversionSettings settings)
    {
        return Formats.All
            .Where(target => target != source && CanConvert(source, target, settings))
            .OrderBy(k => k.Category().ToString(), System.StringComparer.Ordinal)
            .ThenBy(k => k.DisplayName(), System.StringComparer.Ordinal)
            .ToArray();
    }

    private static ConversionPlan? SofficeRoute(FileKind source, FileKind target, string inputPath, string outputPath)
    {
        if (target == FileKind.Pdf && SofficeReadable.Contains(source))
            return SofficePlan(target, inputPath, outputPath);
        if (SofficeTextFamily.Contains(source) && SofficeTextFamily.Contains(target))
            return SofficePlan(target, inputPath, outputPath);
        if (SofficeSheetFamily.Contains(source) && SofficeSheetFamily.Contains(target))
            return SofficePlan(target, inputPath, outputPath);
        if (SofficePresFamily.Contains(source) && SofficePresFamily.Contains(target))
            return SofficePlan(target, inputPath, outputPath);
        return null;
    }

    private static ConversionPlan SofficePlan(FileKind target, string inputPath, string outputPath)
    {
        // Isolated user profile so our headless soffice doesn't fight a user's open LibreOffice.
        var profileDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ULTIMATE-FILE-CONVERTER-soffice");
        var profileUri = new System.Uri(profileDir).AbsoluteUri; // file:///C:/.../ULTIMATE-FILE-CONVERTER-soffice
        return ConversionPlan.Single(Tool.Soffice,
            new[]
            {
                $"-env:UserInstallation={profileUri}",
                "--headless",
                "--convert-to", target.CanonicalExtension(),
                "--outdir", "{OUTPUT_DIR}",
                "{INPUT}",
            },
            inputPath, outputPath);
    }

    /// <summary>
    /// Pandoc-supported document pairs. <c>tex</c> is allowed for text-to-text but PDF output
    /// is deliberately excluded (it would need a TeX engine we don't ship; PDF goes via soffice).
    /// </summary>
    private static bool IsPandocPair(FileKind source, FileKind target)
        => PandocKinds.Contains(source) && PandocKinds.Contains(target);

    // MARK: - Experimental mode (raw-byte fallback)

    private static ConversionPlan WeirdRoute(FileKind target, string inputPath, string outputPath, ConversionSettings settings)
    {
        return target.Category() switch
        {
            FileCategory.Image => RawBytesToImage(target, inputPath, outputPath, settings),
            FileCategory.Audio => ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-f", "u8", "-ar", "22050", "-ac", "1", "-i", "{INPUT}", "{OUTPUT}" },
                inputPath, outputPath),
            FileCategory.Video => ConversionPlan.Single(Tool.Ffmpeg,
                new[] { "-y", "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "256x256",
                        "-framerate", "10", "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}" },
                inputPath, outputPath),
            // No raw decode makes sense for these — copy the bytes under the new extension.
            _ => ConversionPlan.Single(Tool.Copy, new[] { "{INPUT}", "{OUTPUT}" }, inputPath, outputPath),
        };
    }

    private static ConversionPlan RawBytesToImage(FileKind target, string inputPath, string outputPath, ConversionSettings settings)
    {
        // 256x256 grayscale = 64 KiB / frame, fits inside most small files.
        var ffmpegArgs = new[]
        {
            "-y", "-f", "rawvideo", "-pixel_format", "gray", "-video_size", "256x256",
            "-i", "{INPUT}", "-frames:v", "1", "{OUTPUT}",
        };

        if (target == FileKind.Png)
        {
            return ConversionPlan.Single(Tool.Ffmpeg, ffmpegArgs, inputPath, outputPath);
        }

        // ffmpeg writes PNG, ImageMagick transcodes it to the final target format.
        var intermediate = System.IO.Path.Combine(AppPathsTemp(), $"{System.Guid.NewGuid():N}.png");
        return new ConversionPlan(
            new ConversionStep(Tool.Ffmpeg, ffmpegArgs, inputPath, intermediate),
            new ConversionStep(Tool.Magick,
                new[] { "{INPUT}[0]", "-quality", settings.ImageQuality.ToString(), "{OUTPUT}" },
                intermediate, outputPath));
    }

    private static string AppPathsTemp() => Services.AppPaths.TempFolder;
}
