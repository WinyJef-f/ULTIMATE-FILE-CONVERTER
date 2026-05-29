using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimateFileConverter.WinUI.Models;

public sealed class QueueItem : INotifyPropertyChanged
{
    private string _status = "Queued";
    private string? _errorMessage;
    private bool _isRunning;

    public QueueItem(string sourcePath, FileKind sourceKind)
    {
        SourcePath = sourcePath;
        SourceKind = sourceKind;
    }

    public string SourcePath { get; }
    public string FileName => Path.GetFileName(SourcePath);
    public FileKind SourceKind { get; }
    public string? OutputPath { get; set; }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
