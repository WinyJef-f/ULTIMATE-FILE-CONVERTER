using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimateFileConverter.WinUI.Models;

public enum QueueItemStatus
{
    Pending,
    Converting,
    Done,
    Failed,
}

/// <summary>
/// One file in the conversion queue. Port of the macOS <c>QueueItem</c> + its status enum,
/// with presentation-friendly computed properties so the WinUI templates can bind directly.
/// </summary>
public sealed class QueueItem : INotifyPropertyChanged
{
    private QueueItemStatus _status = QueueItemStatus.Pending;
    private FileKind _targetKind;
    private string? _outputPath;
    private string? _errorMessage;
    private long? _fileSizeBytes;

    public QueueItem(string path, FileKind sourceKind, FileKind targetKind)
    {
        Id = System.Guid.NewGuid();
        Path = path;
        SourceKind = sourceKind;
        _targetKind = targetKind;
    }

    public System.Guid Id { get; }
    public string Path { get; }
    public FileKind SourceKind { get; }

    public string DisplayName => System.IO.Path.GetFileName(Path);

    public FileKind TargetKind
    {
        get => _targetKind;
        set
        {
            if (SetField(ref _targetKind, value))
            {
                OnPropertyChanged(nameof(TargetDisplay));
                OnPropertyChanged(nameof(MetadataLine));
            }
        }
    }

    public QueueItemStatus Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(IsConverting));
                OnPropertyChanged(nameof(ShowStatusGlyph));
                OnPropertyChanged(nameof(IsFinished));
                OnPropertyChanged(nameof(IsDone));
                OnPropertyChanged(nameof(IsFailed));
            }
        }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set => SetField(ref _outputPath, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    // MARK: - Derived presentation properties

    public string CategoryGlyph => SourceKind.Category().Glyph();
    public string SourceDisplay => SourceKind.DisplayName();
    public string TargetDisplay => TargetKind.DisplayName();

    /// <summary>"JPEG → PNG · 1.2 MB" — matches the macOS metadata line.</summary>
    public string MetadataLine
    {
        get
        {
            var line = $"{SourceDisplay}  →  {TargetDisplay}";
            var size = FormattedFileSize;
            return size is null ? line : $"{line}   ·   {size}";
        }
    }

    public bool IsConverting => Status == QueueItemStatus.Converting;
    public bool ShowStatusGlyph => Status != QueueItemStatus.Converting;
    public bool IsFinished => Status is QueueItemStatus.Done or QueueItemStatus.Failed;
    public bool IsDone => Status == QueueItemStatus.Done;
    public bool IsFailed => Status == QueueItemStatus.Failed;
    public bool IsPending => Status == QueueItemStatus.Pending;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string StatusText => Status switch
    {
        QueueItemStatus.Pending => "Pending",
        QueueItemStatus.Converting => "Converting",
        QueueItemStatus.Done => "Done",
        QueueItemStatus.Failed => "Failed",
        _ => string.Empty,
    };

    /// <summary>Segoe Fluent / MDL2 glyph used in the status badge.</summary>
    public string StatusGlyph => Status switch
    {
        QueueItemStatus.Pending => "\uE823",  // Clock (waiting)
        QueueItemStatus.Done => "\uE930",     // Completed (filled circle check)
        QueueItemStatus.Failed => "\uE7BA",   // Warning
        _ => string.Empty,
    };

    public string? FormattedFileSize
    {
        get
        {
            _fileSizeBytes ??= TryReadFileSize();
            return _fileSizeBytes is { } bytes ? FormatBytes(bytes) : null;
        }
    }

    private long? TryReadFileSize()
    {
        try
        {
            var info = new System.IO.FileInfo(Path);
            return info.Exists ? info.Length : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "bytes", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1000 && unit < units.Length - 1)
        {
            size /= 1000;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
