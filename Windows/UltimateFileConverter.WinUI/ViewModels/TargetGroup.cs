using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.WinUI.ViewModels;

/// <summary>A category section of selectable target formats, used to build the grouped "Convert to" menu.</summary>
public sealed record TargetGroup(FileCategory Category, IReadOnlyList<FileKind> Items)
{
    public string Header => Category.DisplayName();
}
