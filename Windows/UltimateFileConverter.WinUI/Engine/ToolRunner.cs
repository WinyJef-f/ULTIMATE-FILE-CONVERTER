using System.Collections.Concurrent;
using System.Diagnostics;

namespace UltimateFileConverter.WinUI.Engine;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

/// <summary>Raised when a routed tool can't be found on the machine.</summary>
public sealed class ToolNotFoundException : System.Exception
{
    public Tool Tool { get; }

    public ToolNotFoundException(Tool tool)
        : base($"{tool.FriendlyName()} ({tool.ExecutableName()}) is not installed. " +
               $"Run first-run setup, or install it with: winget install --id {tool.WingetPackageId()} -e")
    {
        Tool = tool;
    }
}

/// <summary>
/// Executes <see cref="ConversionPlan"/>s by launching each step's tool as a child process.
/// Port of the macOS <c>ToolRunner</c>: stops on the first failed step, and cancellation
/// terminates the running child process (and its tree) promptly.
/// </summary>
public static class ToolRunner
{
    // Cache only successful resolutions so a tool installed mid-session is still found on the next miss.
    private static readonly ConcurrentDictionary<Tool, string> ResolvedCache = new();

    public static async Task<ProcessResult> ExecuteAsync(ConversionPlan plan, CancellationToken cancellationToken)
    {
        var last = new ProcessResult(0, string.Empty, string.Empty);
        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Experimental-mode raw copy is handled in-process — no subprocess.
            if (step.Tool == Tool.Copy)
            {
                System.IO.File.Copy(step.InputPath, step.OutputPath, overwrite: true);
                last = new ProcessResult(0, string.Empty, string.Empty);
                cancellationToken.ThrowIfCancellationRequested();
                continue;
            }

            var executable = ResolveExecutable(step.Tool) ?? throw new ToolNotFoundException(step.Tool);
            last = await RunAsync(executable, step.ResolveArguments(), cancellationToken).ConfigureAwait(false);
            if (!last.Success) return last;
            cancellationToken.ThrowIfCancellationRequested();
        }
        return last;
    }

    public static async Task<ProcessResult> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        // Read both pipes concurrently to avoid a full-buffer deadlock on chatty tools (ffmpeg).
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using (cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process may already have exited between the check and the kill.
            }
        }))
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    public static bool IsAvailable(Tool tool) => tool == Tool.Copy || ResolveExecutable(tool) is not null;

    /// <summary>
    /// Locates a tool's executable. Search order: process cache, PATH, the winget shim
    /// directory, fixed install locations, and finally a bounded recursive scan of the
    /// likely roots (winget package store, Program Files, LibreOffice's program folder).
    /// </summary>
    public static string? ResolveExecutable(Tool tool)
    {
        if (tool == Tool.Copy) return "<copy>";
        if (ResolvedCache.TryGetValue(tool, out var cached) && System.IO.File.Exists(cached)) return cached;

        var exe = tool.ExecutableName();

        var resolved =
            FindOnPath(exe)
            ?? FindInWingetLinks(exe)
            ?? CandidatePaths(tool).FirstOrDefault(System.IO.File.Exists)
            ?? SearchRoots(tool).Select(root => SafeFindFirst(root, exe, maxDepth: 6)).FirstOrDefault(p => p is not null);

        if (resolved is not null) ResolvedCache[tool] = resolved;
        return resolved;
    }

    public static string? FindOnPath(string fileName)
    {
        var path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(System.IO.Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = System.IO.Path.Combine(dir.Trim('"'), fileName);
                if (System.IO.File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }
        return null;
    }

    private static string? FindInWingetLinks(string fileName)
    {
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var link = System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Links", fileName);
        return System.IO.File.Exists(link) ? link : null;
    }

    /// <summary>
    /// General executable lookup used for dependency probing (e.g. winget itself, or
    /// Ghostscript, which isn't a routed <see cref="Tool"/>). Searches PATH, the winget shim
    /// directory, the caller's hint roots, and the winget package store.
    /// </summary>
    public static string? FindExecutable(string exe, IEnumerable<string>? extraRoots = null)
    {
        var direct = FindOnPath(exe) ?? FindInWingetLinks(exe);
        if (direct is not null) return direct;

        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var wingetPackages = System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");

        var roots = (extraRoots ?? System.Linq.Enumerable.Empty<string>()).Append(wingetPackages);
        foreach (var root in roots)
        {
            var hit = SafeFindFirst(root, exe, maxDepth: 6);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static IEnumerable<string> CandidatePaths(Tool tool)
    {
        var programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

        switch (tool)
        {
            case Tool.Soffice:
                yield return System.IO.Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe");
                yield return System.IO.Path.Combine(programFilesX86, "LibreOffice", "program", "soffice.exe");
                break;
            case Tool.Pandoc:
                yield return System.IO.Path.Combine(localAppData, "Pandoc", "pandoc.exe");
                yield return System.IO.Path.Combine(programFiles, "Pandoc", "pandoc.exe");
                break;
        }
    }

    private static IEnumerable<string> SearchRoots(Tool tool)
    {
        var programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var wingetPackages = System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");

        switch (tool)
        {
            case Tool.Ffmpeg:
                yield return wingetPackages;
                break;
            case Tool.Magick:
                yield return programFiles;       // ImageMagick-7.x.x-Q16-HDRI\magick.exe
                yield return programFilesX86;
                yield return wingetPackages;
                break;
            case Tool.Pandoc:
                yield return wingetPackages;
                break;
            case Tool.Soffice:
                yield return System.IO.Path.Combine(programFiles, "LibreOffice");
                yield return System.IO.Path.Combine(programFilesX86, "LibreOffice");
                break;
        }
    }

    private static string? SafeFindFirst(string root, string fileName, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root)) return null;

        var queue = new Queue<(string Dir, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (dir, depth) = queue.Dequeue();
            try
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(dir, fileName))
                {
                    return file;
                }
            }
            catch
            {
                // Inaccessible directory — skip.
            }

            if (depth >= maxDepth) continue;
            try
            {
                foreach (var sub in System.IO.Directory.EnumerateDirectories(dir))
                {
                    queue.Enqueue((sub, depth + 1));
                }
            }
            catch
            {
                // Skip directories we can't enumerate.
            }
        }
        return null;
    }
}
