using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UltimateFileConverter.WinUI.Engine;
using UltimateFileConverter.WinUI.Models;
using UltimateFileConverter.WinUI.Services;

namespace UltimateFileConverter.WinUI.ViewModels;

/// <summary>
/// Owns the queue, settings, and persistent history, and drives conversions.
/// Direct port of the macOS <c>AppViewModel</c>. Everything runs on the UI thread; the
/// CPU work happens in child processes that are awaited, so the UI stays responsive.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int MaxHistory = 100;

    private ConversionSettings _settings;
    private FileKind? _targetFormat;
    private bool _isConverting;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        _settings = SettingsStore.Load();
        Queue.CollectionChanged += (_, _) => RaiseQueueDerived();
        foreach (var entry in HistoryStore.Load())
        {
            History.Add(entry);
        }
        History.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(ShowEmptyState));
    }

    public ObservableCollection<QueueItem> Queue { get; } = new();
    public ObservableCollection<HistoryEntry> History { get; } = new();

    public ConversionSettings Settings => _settings;

    public FileKind? TargetFormat
    {
        get => _targetFormat;
        set
        {
            if (_targetFormat == value) return;
            _targetFormat = value;
            ApplyTargetToAll();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(TargetFormatDisplay));
            RaisePropertyChanged(nameof(CanConvert));
        }
    }

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetField(ref _isConverting, value))
            {
                RaisePropertyChanged(nameof(CanConvert));
                RaisePropertyChanged(nameof(CanClearQueue));
                RaisePropertyChanged(nameof(CanCancel));
            }
        }
    }

    // MARK: - Derived UI state

    public bool ShowEmptyState => Queue.Count == 0 && History.Count == 0;
    public bool HasQueue => Queue.Count > 0;
    public bool HasHistory => History.Count > 0;
    public bool HasFinishedItems => Queue.Any(i => i.IsFinished);
    public bool HasPendingItems => Queue.Any(i => i.IsPending);
    public int PendingCount => Queue.Count(i => i.IsPending);
    public string QueueCountText => $"{Queue.Count} file{(Queue.Count == 1 ? "" : "s")}";
    public string HistoryCountText => History.Count.ToString();

    public bool CanConvert => HasPendingItems && _targetFormat is not null && !IsConverting;
    public bool CanCancel => IsConverting;
    public bool CanClearQueue => Queue.Count > 0 && !IsConverting;

    public string ConvertButtonText
    {
        get
        {
            var n = PendingCount;
            return n == 0 ? "Convert" : $"Convert {n} file{(n == 1 ? "" : "s")}";
        }
    }

    public string TargetFormatDisplay => _targetFormat?.DisplayName() ?? "Choose format";
    public bool HasTargetOptions => ValidTargets.Count > 0;

    /// <summary>Intersection of valid targets across every active queue item (matches macOS).</summary>
    public IReadOnlyList<FileKind> ValidTargets
    {
        get
        {
            var activeKinds = Queue.Where(i => !i.IsFinished).Select(i => i.SourceKind).Distinct().ToList();
            if (activeKinds.Count == 0) return System.Array.Empty<FileKind>();

            HashSet<FileKind>? common = null;
            foreach (var kind in activeKinds)
            {
                var valid = ConversionRouter.ValidTargets(kind, _settings).ToHashSet();
                common = common is null ? valid : Intersect(common, valid);
            }

            return (common ?? new HashSet<FileKind>())
                .OrderBy(k => k.Category().ToString(), System.StringComparer.Ordinal)
                .ThenBy(k => k.DisplayName(), System.StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>Valid targets grouped by category in enum order, for the grouped picker menu.</summary>
    public IReadOnlyList<TargetGroup> ValidTargetGroups
    {
        get
        {
            var byCategory = ValidTargets.GroupBy(k => k.Category()).ToDictionary(g => g.Key, g => g.ToList());
            var groups = new List<TargetGroup>();
            foreach (var category in Formats.AllCategories)
            {
                if (byCategory.TryGetValue(category, out var items) && items.Count > 0)
                {
                    items.Sort((a, b) => string.CompareOrdinal(a.DisplayName(), b.DisplayName()));
                    groups.Add(new TargetGroup(category, items));
                }
            }
            return groups;
        }
    }

    // MARK: - Queue management

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = new List<QueueItem>();
        foreach (var path in paths)
        {
            var kind = FormatDetector.Detect(path);
            if (kind is not FileKind sourceKind) continue;
            if (Queue.Any(i => string.Equals(i.Path, path, System.StringComparison.OrdinalIgnoreCase) && !i.IsFinished)) continue;

            FileKind initialTarget;
            if (_targetFormat is FileKind tf && ConversionRouter.CanConvert(sourceKind, tf, _settings))
            {
                initialTarget = tf;
            }
            else
            {
                var valid = ConversionRouter.ValidTargets(sourceKind, _settings);
                initialTarget = valid.Count > 0 ? valid[0] : sourceKind;
            }

            added.Add(new QueueItem(path, sourceKind, initialTarget));
        }

        foreach (var item in added)
        {
            item.PropertyChanged += OnQueueItemPropertyChanged;
            Queue.Add(item);
        }

        if (_targetFormat is null && Queue.Count > 0)
        {
            TargetFormat = FirstOrNull(ConversionRouter.ValidTargets(Queue[0].SourceKind, _settings));
            ApplyTargetToAll();
        }

        RaiseQueueDerived();
    }

    public void Remove(QueueItem item)
    {
        item.PropertyChanged -= OnQueueItemPropertyChanged;
        Queue.Remove(item);
        if (Queue.Count == 0) TargetFormat = null;
        RaiseQueueDerived();
    }

    public void ClearQueue()
    {
        foreach (var item in Queue) item.PropertyChanged -= OnQueueItemPropertyChanged;
        Queue.Clear();
        TargetFormat = null;
        RaiseQueueDerived();
    }

    public void ClearFinished()
    {
        foreach (var item in Queue.Where(i => i.IsFinished).ToList())
        {
            item.PropertyChanged -= OnQueueItemPropertyChanged;
            Queue.Remove(item);
        }
        if (Queue.Count == 0) TargetFormat = null;
        RaiseQueueDerived();
    }

    /// <summary>Apply the current target to every active item that supports it.</summary>
    public void ApplyTargetToAll()
    {
        if (_targetFormat is not FileKind target) return;
        foreach (var item in Queue)
        {
            if (!item.IsFinished && ConversionRouter.CanConvert(item.SourceKind, target, _settings))
            {
                item.TargetKind = target;
            }
        }
    }

    // MARK: - Settings

    public void ApplySettings(ConversionSettings updated)
    {
        var weirdChanged = _settings.WeirdModeEnabled != updated.WeirdModeEnabled;
        _settings = updated;
        SettingsStore.Save(_settings);
        RaisePropertyChanged(nameof(Settings));

        if (weirdChanged)
        {
            RaisePropertyChanged(nameof(ValidTargets));
            RaisePropertyChanged(nameof(ValidTargetGroups));
            RaisePropertyChanged(nameof(HasTargetOptions));
            ReconcileTargetAfterWeirdToggle();
        }
    }

    private void ReconcileTargetAfterWeirdToggle()
    {
        var valid = ValidTargets;
        if (_targetFormat is FileKind current && !valid.Contains(current))
        {
            TargetFormat = FirstOrNull(valid);
        }
        else if (_targetFormat is null && Queue.Count > 0)
        {
            TargetFormat = FirstOrNull(valid);
        }
    }

    // MARK: - Conversion

    public async Task ConvertAsync()
    {
        if (IsConverting) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsConverting = true;
        try
        {
            ApplyTargetToAll();
            var pendingIds = Queue.Where(i => i.IsPending).Select(i => i.Id).ToList();
            foreach (var id in pendingIds)
            {
                if (token.IsCancellationRequested) break;
                await ConvertItemAsync(id, token);
            }
        }
        finally
        {
            IsConverting = false;
            // Any row still 'converting' was interrupted — reset it to pending.
            foreach (var item in Queue.Where(i => i.IsConverting))
            {
                item.Status = QueueItemStatus.Pending;
            }
            RaiseQueueDerived();
        }
    }

    public void CancelConversion()
    {
        _cts?.Cancel();
        IsConverting = false;
        foreach (var item in Queue.Where(i => i.IsConverting))
        {
            item.Status = QueueItemStatus.Pending;
        }
        RaiseQueueDerived();
    }

    private async Task ConvertItemAsync(System.Guid id, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        var item = Queue.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        item.ErrorMessage = null;
        item.Status = QueueItemStatus.Converting;

        var outputPath = ComputeOutputPath(item);
        var plan = ConversionRouter.Plan(item.SourceKind, item.TargetKind, item.Path, outputPath, _settings);
        if (plan is null)
        {
            Finalize(item, null, $"No conversion path from {item.SourceKind.DisplayName()} to {item.TargetKind.DisplayName()}.", false);
            return;
        }

        try
        {
            var result = await ToolRunner.ExecuteAsync(plan, token);
            if (token.IsCancellationRequested) return;

            var success = result.Success && System.IO.File.Exists(plan.OutputPath);
            if (success)
            {
                Finalize(item, plan.OutputPath, null, true);
            }
            else
            {
                var raw = string.IsNullOrEmpty(result.StandardError) ? result.StandardOutput : result.StandardError;
                var msg = raw.Trim();
                Finalize(item, null, msg.Length == 0 ? $"Conversion failed (exit {result.ExitCode})." : msg, false);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Cancelled — ConvertAsync's finally resets the row to pending.
        }
        catch (System.Exception ex)
        {
            if (token.IsCancellationRequested) return;
            Finalize(item, null, ex.Message, false);
        }
    }

    private void Finalize(QueueItem item, string? output, string? message, bool success)
    {
        item.OutputPath = success ? output : null;
        item.ErrorMessage = success ? null : message;
        item.Status = success ? QueueItemStatus.Done : QueueItemStatus.Failed;

        long bytes = 0;
        try { bytes = new System.IO.FileInfo(item.Path).Length; } catch { }

        AddHistoryEntry(HistoryEntry.Create(
            item.Path, item.SourceKind, success ? output : null, item.TargetKind, success, success ? null : message, bytes));
        RaiseQueueDerived();
    }

    public ConversionStats Stats => ConversionStats.Compute(History);

    private string ComputeOutputPath(QueueItem item)
    {
        var dir = _settings.HasUsableCustomFolder
            ? _settings.CustomOutputFolderPath!
            : System.IO.Path.GetDirectoryName(item.Path) ?? System.IO.Directory.GetCurrentDirectory();
        var baseName = System.IO.Path.GetFileNameWithoutExtension(item.Path);
        return System.IO.Path.Combine(dir, $"{baseName}.{item.TargetKind.CanonicalExtension()}");
    }

    // MARK: - History

    public void ClearHistory()
    {
        History.Clear();
        HistoryStore.Save(History);
        RaisePropertyChanged(nameof(HasHistory));
        RaisePropertyChanged(nameof(HistoryCountText));
        RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private void AddHistoryEntry(HistoryEntry entry)
    {
        History.Insert(0, entry);
        while (History.Count > MaxHistory)
        {
            History.RemoveAt(History.Count - 1);
        }
        HistoryStore.Save(History);
        RaisePropertyChanged(nameof(HasHistory));
        RaisePropertyChanged(nameof(HistoryCountText));
        RaisePropertyChanged(nameof(ShowEmptyState));
    }

    // MARK: - Helpers

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueueItem.Status))
        {
            RaiseQueueDerived();
        }
    }

    private void RaiseQueueDerived()
    {
        RaisePropertyChanged(nameof(ShowEmptyState));
        RaisePropertyChanged(nameof(HasQueue));
        RaisePropertyChanged(nameof(HasFinishedItems));
        RaisePropertyChanged(nameof(HasPendingItems));
        RaisePropertyChanged(nameof(PendingCount));
        RaisePropertyChanged(nameof(QueueCountText));
        RaisePropertyChanged(nameof(CanConvert));
        RaisePropertyChanged(nameof(CanClearQueue));
        RaisePropertyChanged(nameof(ConvertButtonText));
        RaisePropertyChanged(nameof(ValidTargets));
        RaisePropertyChanged(nameof(ValidTargetGroups));
        RaisePropertyChanged(nameof(HasTargetOptions));
    }

    private static FileKind? FirstOrNull(IReadOnlyList<FileKind> kinds) => kinds.Count > 0 ? kinds[0] : null;

    private static HashSet<FileKind> Intersect(HashSet<FileKind> a, HashSet<FileKind> b)
    {
        a.IntersectWith(b);
        return a;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        RaisePropertyChanged(name);
        return true;
    }

    private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
