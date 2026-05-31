using System.Collections.Generic;
using System.Linq;

namespace UltimateFileConverter.WinUI.Models;

/// <summary>An entry in the top-formats or top-pairs bar list, pre-computed for binding.</summary>
public sealed record StatBarItem(string Label, int Count, double MaxValue)
{
    public string CountText => Count.ToString();
    public double DoubleCount => (double)Count;
}

/// <summary>Aggregated stats computed from the full history list.</summary>
public sealed class ConversionStats
{
    public int TotalSuccesses { get; init; }
    public int TotalFailures { get; init; }
    public long TotalBytesProcessed { get; init; }
    public IReadOnlyList<StatBarItem> TopFormatsItems { get; init; } = System.Array.Empty<StatBarItem>();
    public IReadOnlyList<StatBarItem> TopPairsItems { get; init; } = System.Array.Empty<StatBarItem>();

    public string TotalSuccessesText => TotalSuccesses.ToString();
    public string TotalFailuresText => TotalFailures.ToString();
    public string FormattedBytes => FormatBytes(TotalBytesProcessed);

    public static ConversionStats Compute(IEnumerable<HistoryEntry> history)
    {
        var all = history.ToList();
        var successes = all.Where(e => e.IsSuccess).ToList();
        var failures = all.Where(e => !e.IsSuccess).ToList();

        var totalBytes = successes.Sum(e => e.BytesProcessed);

        var srcCounts = successes
            .Where(e => e.SourceKindEnum.HasValue)
            .GroupBy(e => e.SourceKindEnum!.Value)
            .Select(g => (Kind: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var pairCounts = successes
            .Where(e => e.SourceKindEnum.HasValue && e.OutputKindEnum.HasValue)
            .GroupBy(e => (Src: e.SourceKindEnum!.Value, Tgt: e.OutputKindEnum!.Value))
            .Select(g => (Label: $"{g.Key.Src.DisplayName()} → {g.Key.Tgt.DisplayName()}", Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        double srcMax = srcCounts.Count > 0 ? srcCounts[0].Count : 1;
        double pairMax = pairCounts.Count > 0 ? pairCounts[0].Count : 1;

        return new ConversionStats
        {
            TotalSuccesses = successes.Count,
            TotalFailures = failures.Count,
            TotalBytesProcessed = totalBytes,
            TopFormatsItems = srcCounts.Select(x => new StatBarItem(x.Kind.DisplayName(), x.Count, srcMax)).ToList(),
            TopPairsItems = pairCounts.Select(x => new StatBarItem(x.Label, x.Count, pairMax)).ToList(),
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
