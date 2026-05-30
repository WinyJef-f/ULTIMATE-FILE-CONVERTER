using UltimateFileConverter.WinUI.Engine;

namespace UltimateFileConverter.WinUI.Services;

/// <summary>A third-party CLI tool the app relies on, plus how to find and install it.</summary>
public sealed record Dependency(
    string FriendlyName,
    string WingetId,
    string ProbeExecutable,
    System.Collections.Generic.IReadOnlyList<string> ExtraRoots,
    string Purpose)
{
    public bool IsInstalled => ToolRunner.FindExecutable(ProbeExecutable, ExtraRoots) is not null;
}

/// <summary>
/// First-run setup: the macOS app ships or locates its tools via Homebrew; on Windows we
/// install the equivalent tools with <c>winget</c>. Detection probes PATH, the winget shim
/// directory, the winget package store, and the usual install locations, so freshly installed
/// tools are usually found without needing an app restart.
/// </summary>
public sealed class DependencyService
{
    public static IReadOnlyList<Dependency> Required { get; } = BuildRequired();

    private static IReadOnlyList<Dependency> BuildRequired()
    {
        var programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

        return new[]
        {
            new Dependency("FFmpeg", "Gyan.FFmpeg", "ffmpeg.exe",
                System.Array.Empty<string>(),
                "Audio, video, and cross-media conversions."),
            new Dependency("ImageMagick", "ImageMagick.ImageMagick", "magick.exe",
                new[] { programFiles, programFilesX86 },
                "Image conversion plus SVG and PDF rasterization."),
            new Dependency("MuPDF (mutool)", "ArtifexSoftware.mutool", "mutool.exe",
                System.Array.Empty<string>(),
                "PDF to image rasterization."),
            new Dependency("Pandoc", "JohnMacFarlane.Pandoc", "pandoc.exe",
                new[] { System.IO.Path.Combine(localAppData, "Pandoc"), System.IO.Path.Combine(programFiles, "Pandoc") },
                "Markup document conversions (Markdown, HTML, DOCX, EPUB, …)."),
            new Dependency("LibreOffice", "TheDocumentFoundation.LibreOffice", "soffice.exe",
                new[] { System.IO.Path.Combine(programFiles, "LibreOffice"), System.IO.Path.Combine(programFilesX86, "LibreOffice") },
                "Office formats and PDF export."),
        };
    }

    public IReadOnlyList<Dependency> Missing() => Required.Where(d => !d.IsInstalled).ToList();

    public bool AllInstalled() => Required.All(d => d.IsInstalled);

    public bool MarkerExists => System.IO.File.Exists(AppPaths.DependencyMarkerFile);

    /// <summary>True when we should offer first-run setup (no marker yet, and something is missing).</summary>
    public bool ShouldOfferFirstRunSetup() => !MarkerExists && !AllInstalled();

    public void WriteMarker()
    {
        try
        {
            System.IO.File.WriteAllText(AppPaths.DependencyMarkerFile, System.DateTimeOffset.UtcNow.ToString("O"));
        }
        catch
        {
            // Non-fatal.
        }
    }

    public static string? FindWinget()
    {
        var onPath = ToolRunner.FindOnPath("winget.exe");
        if (onPath is not null) return onPath;

        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var alias = System.IO.Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return System.IO.File.Exists(alias) ? alias : null;
    }

    /// <summary>
    /// Installs every missing dependency with winget, reporting progress lines. Returns true
    /// only if all required tools are present afterwards. Best-effort: one failed package does
    /// not abort the rest, so the user gets as many working tools as possible.
    /// </summary>
    public async Task<bool> InstallMissingAsync(IProgress<string> log, CancellationToken cancellationToken)
    {
        var missing = Missing();
        if (missing.Count == 0)
        {
            log.Report("All conversion tools are already installed.");
            WriteMarker();
            return true;
        }

        var winget = FindWinget();
        if (winget is null)
        {
            log.Report("winget was not found. Install \"App Installer\" from the Microsoft Store, then run setup again.");
            return false;
        }

        log.Report($"Installing {missing.Count} tool(s) with winget. Windows may prompt for permission per package.");

        foreach (var dep in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dep.IsInstalled)
            {
                log.Report($"✓ {dep.FriendlyName} is already installed.");
                continue;
            }

            log.Report($"Installing {dep.FriendlyName} ({dep.WingetId})…");
            ProcessResult result;
            try
            {
                result = await ToolRunner.RunAsync(winget, new[]
                {
                    "install", "--id", dep.WingetId, "--exact",
                    "--source", "winget",
                    "--accept-package-agreements", "--accept-source-agreements",
                    "--disable-interactivity", "--silent",
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                log.Report($"✗ {dep.FriendlyName}: could not start winget ({ex.Message}).");
                continue;
            }

            // winget's exit code varies ("already installed" etc.), so trust a fresh probe.
            if (dep.IsInstalled)
            {
                log.Report($"✓ {dep.FriendlyName} installed.");
            }
            else
            {
                var detail = FirstMeaningfulLine(result.StandardError) ?? FirstMeaningfulLine(result.StandardOutput);
                log.Report($"✗ {dep.FriendlyName} did not install (winget exit {result.ExitCode}). {detail}".TrimEnd());
            }
        }

        var ok = AllInstalled();
        if (ok)
        {
            WriteMarker();
            log.Report("All conversion tools are ready.");
        }
        else
        {
            var still = Missing().Select(d => d.FriendlyName);
            log.Report($"Setup finished with some tools still missing: {string.Join(", ", still)}. " +
                       "You can rerun setup, or install them manually, then restart the app.");
        }
        return ok;
    }

    private static string? FirstMeaningfulLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) return trimmed;
        }
        return null;
    }
}
