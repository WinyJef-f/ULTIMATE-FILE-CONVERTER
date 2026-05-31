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

        // --- Subtitle -> Subtitle ---
        // ffmpeg handles srt/ass/vtt directly. SBV has poor ffmpeg support, so it is
        // bridged through SRT in-process (Tool.Subtitle), then ffmpeg reaches ass/vtt.
        if (src == FileCategory.Subtitle && dst == FileCategory.Subtitle)
        {
            return SubtitleRoute(source, target, inputPath, outputPath);
        }

        // --- Archive -> Archive via 7-Zip ---
        // All conversions go through a temp extract-then-recompress pipeline.
        // tar.gz targets need an extra step: create a .tar first, then gzip it.
        if (src == FileCategory.Archive && dst == FileCategory.Archive)
        {
            return ArchiveRoute(source, target, inputPath, outputPath);
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

    /// <summary>
    /// Builds a subtitle→subtitle plan. srt/ass/vtt go straight through ffmpeg; SBV is
    /// translated to/from SRT in-process first (ffmpeg's SBV support is unreliable).
    /// </summary>
    private static ConversionPlan SubtitleRoute(FileKind source, FileKind target, string inputPath, string outputPath)
    {
        // SBV source: SBV → SRT (in-process), then SRT → target via ffmpeg if needed.
        if (source == FileKind.Sbv)
        {
            if (target == FileKind.Srt)
            {
                return ConversionPlan.Single(Tool.Subtitle,
                    new[] { "sbv2srt", "{INPUT}", "{OUTPUT}" },
                    inputPath, outputPath);
            }
            var srt = System.IO.Path.Combine(AppPathsTemp(), $"{System.Guid.NewGuid():N}.srt");
            return new ConversionPlan(
                new ConversionStep(Tool.Subtitle,
                    new[] { "sbv2srt", "{INPUT}", "{OUTPUT}" },
                    inputPath, srt),
                new ConversionStep(Tool.Ffmpeg,
                    new[] { "-y", "-i", "{INPUT}", "{OUTPUT}" },
                    srt, outputPath));
        }

        // SBV target: source → SRT via ffmpeg if needed, then SRT → SBV (in-process).
        if (target == FileKind.Sbv)
        {
            if (source == FileKind.Srt)
            {
                return ConversionPlan.Single(Tool.Subtitle,
                    new[] { "srt2sbv", "{INPUT}", "{OUTPUT}" },
                    inputPath, outputPath);
            }
            var srt = System.IO.Path.Combine(AppPathsTemp(), $"{System.Guid.NewGuid():N}.srt");
            return new ConversionPlan(
                new ConversionStep(Tool.Ffmpeg,
                    new[] { "-y", "-i", "{INPUT}", "{OUTPUT}" },
                    inputPath, srt),
                new ConversionStep(Tool.Subtitle,
                    new[] { "srt2sbv", "{INPUT}", "{OUTPUT}" },
                    srt, outputPath));
        }

        // srt ⇄ ass ⇄ vtt: direct ffmpeg.
        return ConversionPlan.Single(Tool.Ffmpeg,
            new[] { "-y", "-i", "{INPUT}", "{OUTPUT}" },
            inputPath, outputPath);
    }

    /// <summary>
    /// Extract-then-recompress pipeline via 7-Zip. All format pairs share the same extract
    /// step; tar.gz targets need a two-step compress (tar first, then gzip).
    /// </summary>
    private static ConversionPlan ArchiveRoute(FileKind source, FileKind target, string inputPath, string outputPath)
    {
        var extractDir = IntermediateDir();

        // Extract step: 7z x -y {INPUT} -o<extractDir>
        // The dummy outputPath here is never used in argument substitution (args have no {OUTPUT}).
        var extractDummy = System.IO.Path.Combine(extractDir, ".done");
        var extractStep = new ConversionStep(Tool.SevenZip,
            new[] { "x", "-y", "{INPUT}", $"-o{extractDir}" },
            inputPath, extractDummy);

        // tar.gz target: 7z can't create .tar.gz in one pass — create .tar then gzip it.
        if (target == FileKind.TarGz)
        {
            var tarTemp = System.IO.Path.Combine(AppPathsTemp(), $"{System.Guid.NewGuid():N}.tar");
            return new ConversionPlan(
                extractStep,
                new ConversionStep(Tool.SevenZip,
                    new[] { "a", "-ttar", "{OUTPUT}", $"{extractDir}/*", "-r" },
                    extractDummy, tarTemp),
                new ConversionStep(Tool.SevenZip,
                    new[] { "a", "-tgzip", "{OUTPUT}", "{INPUT}" },
                    tarTemp, outputPath));
        }

        var formatFlag = target switch
        {
            FileKind.Zip => "-tzip",
            FileKind.SevenZ => "-t7z",
            FileKind.Tar => "-ttar",
            _ => "-tzip",
        };
        return new ConversionPlan(
            extractStep,
            new ConversionStep(Tool.SevenZip,
                new[] { "a", formatFlag, "{OUTPUT}", $"{extractDir}/*", "-r" },
                extractDummy, outputPath));
    }

    /// <summary>Creates and returns a fresh temporary subdirectory for archive extraction.</summary>
    private static string IntermediateDir()
    {
        var dir = System.IO.Path.Combine(AppPathsTemp(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
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
            FileCategory.Archive or _ => ConversionPlan.Single(Tool.Copy, new[] { "{INPUT}", "{OUTPUT}" }, inputPath, outputPath),
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
