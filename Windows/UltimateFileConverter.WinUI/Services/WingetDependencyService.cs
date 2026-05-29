using UltimateFileConverter.WinUI.Engine;

namespace UltimateFileConverter.WinUI.Services;

public sealed class WingetDependencyService
{
    private static readonly Tool[] RequiredTools = [Tool.Ffmpeg, Tool.Magick, Tool.Pandoc, Tool.Soffice, Tool.SevenZip];
    private static readonly string MarkerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ULTIMATE-FILE-CONVERTER", "windows-deps-v1.ok");

    public async Task EnsureFirstRunDependenciesAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (File.Exists(MarkerPath) && RequiredTools.All(tool => ToolRunner.ResolveExecutable(tool) is not null))
        {
            progress.Report("All Windows conversion tools are installed.");
            return;
        }

        var winget = ToolRunner.ResolveExecutableName("winget.exe") ?? FindWinget();
        if (winget is null)
        {
            progress.Report("winget was not found. Install App Installer from Microsoft Store, then restart this app.");
            return;
        }

        foreach (var tool in RequiredTools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ToolRunner.ResolveExecutable(tool) is not null)
            {
                progress.Report($"{tool.ExecutableName()} is already installed.");
                continue;
            }

            progress.Report($"Installing {tool.WingetPackageId()} with winget…");
            var result = await ToolRunner.RunAsync(winget, ["install", "--id", tool.WingetPackageId(), "--exact", "--accept-package-agreements", "--accept-source-agreements", "--silent"], cancellationToken);
            if (!result.Success)
            {
                progress.Report($"winget could not install {tool.WingetPackageId()}: {result.StandardError}{result.StandardOutput}");
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        await File.WriteAllTextAsync(MarkerPath, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
        progress.Report("Windows conversion tools are ready.");
    }

    private static string? FindWinget()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packageRoot = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(packageRoot) ? packageRoot : null;
    }
}
