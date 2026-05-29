using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UltimateFileConverter.WinUI.Engine;
using UltimateFileConverter.WinUI.Models;
using UltimateFileConverter.WinUI.Services;

namespace UltimateFileConverter.WinUI.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ToolRunner _runner = new();
    private readonly WingetDependencyService _dependencyService = new();
    private CancellationTokenSource? _conversionCts;
    private FileKind? _selectedTarget = FileKind.Png;
    private string _statusMessage = "Ready.";
    private bool _dependencyBannerVisible = true;
    private bool _isConverting;

    public MainViewModel()
    {
        AvailableTargets = new ObservableCollection<FileKind>(FileKind.All);
    }

    public ObservableCollection<QueueItem> Queue { get; } = [];
    public ObservableCollection<FileKind> AvailableTargets { get; }
    public ConversionSettings Settings { get; } = new();

    public FileKind? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            SetField(ref _selectedTarget, value);
            OnPropertyChanged(nameof(CanConvert));
        }
    }

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            SetField(ref _isConverting, value);
            OnPropertyChanged(nameof(CanConvert));
        }
    }

    public bool CanConvert => Queue.Count > 0 && SelectedTarget is not null && !IsConverting;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool DependencyBannerVisible
    {
        get => _dependencyBannerVisible;
        private set => SetField(ref _dependencyBannerVisible, value);
    }

    public bool WeirdModeEnabled
    {
        get => Settings.WeirdModeEnabled;
        set
        {
            if (Settings.WeirdModeEnabled == value) return;
            Settings.WeirdModeEnabled = value;
            OnPropertyChanged();
        }
    }

    public double ImageQualityValue
    {
        get => Settings.ImageQuality;
        set { Settings.ImageQuality = (int)Math.Round(value); OnPropertyChanged(); }
    }

    public double AudioBitrateValue
    {
        get => Settings.AudioBitrate;
        set { Settings.AudioBitrate = (int)Math.Round(value); OnPropertyChanged(); }
    }

    public double VideoCRFValue
    {
        get => Settings.VideoCRF;
        set { Settings.VideoCRF = (int)Math.Round(value); OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task EnsureDependenciesAsync()
    {
        var progress = new Progress<string>(message => StatusMessage = message);
        await _dependencyService.EnsureFirstRunDependenciesAsync(progress, CancellationToken.None);
        DependencyBannerVisible = false;
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths.Where(File.Exists))
        {
            var kind = FileKind.FromPath(path);
            if (kind is null)
            {
                StatusMessage = $"Skipped unsupported file: {Path.GetFileName(path)}";
                continue;
            }

            Queue.Add(new QueueItem(path, kind));
            added++;
        }

        StatusMessage = added == 0 ? StatusMessage : $"Added {added} file(s).";
        OnPropertyChanged(nameof(CanConvert));
    }

    public async Task ConvertAsync()
    {
        if (SelectedTarget is null || IsConverting) return;

        _conversionCts = new CancellationTokenSource();
        IsConverting = true;
        try
        {
            foreach (var item in Queue)
            {
                _conversionCts.Token.ThrowIfCancellationRequested();
                item.IsRunning = true;
                item.Status = "Converting…";
                item.ErrorMessage = null;

                var outputPath = BuildOutputPath(item.SourcePath, SelectedTarget);
                var plan = ConversionRouter.Plan(item.SourceKind, SelectedTarget, item.SourcePath, outputPath, Settings);
                if (plan is null)
                {
                    item.Status = "Unsupported";
                    item.ErrorMessage = $"No Windows route from {item.SourceKind.DisplayName} to {SelectedTarget.DisplayName}.";
                    item.IsRunning = false;
                    continue;
                }

                var result = await _runner.ExecuteAsync(plan, _conversionCts.Token);
                item.OutputPath = outputPath;
                item.Status = result.Success ? "Done" : "Failed";
                item.ErrorMessage = result.Success ? null : string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                item.IsRunning = false;
            }

            StatusMessage = "Conversion queue finished.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion cancelled.";
        }
        finally
        {
            foreach (var item in Queue) item.IsRunning = false;
            _conversionCts.Dispose();
            _conversionCts = null;
            IsConverting = false;
        }
    }

    public void Cancel() => _conversionCts?.Cancel();

    private string BuildOutputPath(string sourcePath, FileKind target)
    {
        var outputDir = Settings.OutputFolderMode == OutputFolderMode.CustomFolder && !string.IsNullOrWhiteSpace(Settings.CustomOutputFolderPath)
            ? Settings.CustomOutputFolderPath!
            : Path.GetDirectoryName(sourcePath)!;
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(outputDir, $"{baseName}.{target.CanonicalExtension}");
        var i = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outputDir, $"{baseName} ({i++}).{target.CanonicalExtension}");
        }
        return candidate;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
