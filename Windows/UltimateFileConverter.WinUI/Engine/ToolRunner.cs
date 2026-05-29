using System.Diagnostics;
using System.Text;

namespace UltimateFileConverter.WinUI.Engine;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

public sealed class ToolRunner
{
    public async Task<ProcessResult> ExecuteAsync(ConversionPlan plan, CancellationToken cancellationToken)
    {
        var last = new ProcessResult(0, string.Empty, string.Empty);
        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step.Tool == Tool.Copy)
            {
                File.Copy(step.InputPath, step.OutputPath, overwrite: true);
                last = new ProcessResult(0, string.Empty, string.Empty);
                continue;
            }

            var executable = ResolveExecutable(step.Tool) ?? throw new FileNotFoundException($"{step.Tool.ExecutableName()} was not found. First-run setup should install it with winget package {step.Tool.WingetPackageId()}.");
            last = await RunAsync(executable, step.ResolveArguments(), cancellationToken);
            if (!last.Success) return last;
        }

        return last;
    }

    public static string? ResolveExecutable(Tool tool)
    {
        if (tool == Tool.Copy) return "copy";

        var names = tool == Tool.Soffice ? new[] { "soffice.exe" } : [tool.ExecutableName()];
        foreach (var name in names)
        {
            var fromPath = ResolveExecutableName(name);
            if (fromPath is not null) return fromPath;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = tool switch
        {
            Tool.Ffmpeg => SafeFind(programFiles, "ffmpeg.exe").Take(8),
            Tool.Magick => SafeFind(programFiles, "magick.exe").Take(8),
            Tool.Pandoc => SafeFind(programFiles, "pandoc.exe").Concat(SafeFind(localAppData, "pandoc.exe")).Take(8),
            Tool.Soffice => SafeFind(programFiles, "soffice.exe").Concat(SafeFind(programFilesX86, "soffice.exe")).Take(8),
            Tool.SevenZip => SafeFind(programFiles, "7z.exe").Concat(SafeFind(programFilesX86, "7z.exe")).Take(8),
            _ => []
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cancellation; process may have already exited.
            }
        });

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    public static string? ResolveExecutableName(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> SafeFind(string root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) yield break;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> files = [];
            IEnumerable<string> dirs = [];
            try { files = Directory.EnumerateFiles(current, fileName); } catch { }
            foreach (var file in files) yield return file;
            try { dirs = Directory.EnumerateDirectories(current); } catch { }
            foreach (var dir in dirs) pending.Push(dir);
        }
    }
}
