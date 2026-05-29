namespace UltimateFileConverter.WinUI.Services;

/// <summary>
/// Abbreviated relative-time formatting, equivalent to the macOS
/// <c>RelativeDateTimeFormatter</c> with <c>.abbreviated</c> style ("now", "3m", "2h", "5d").
/// </summary>
public static class RelativeTime
{
    public static string Format(System.DateTimeOffset date)
    {
        var delta = System.DateTimeOffset.Now - date;
        if (delta < System.TimeSpan.Zero) delta = System.TimeSpan.Zero;

        var seconds = delta.TotalSeconds;
        if (seconds < 5) return "now";
        if (seconds < 60) return $"{(int)seconds}s ago";

        var minutes = delta.TotalMinutes;
        if (minutes < 60) return $"{(int)minutes}m ago";

        var hours = delta.TotalHours;
        if (hours < 24) return $"{(int)hours}h ago";

        var days = delta.TotalDays;
        if (days < 7) return $"{(int)days}d ago";
        if (days < 30) return $"{(int)(days / 7)}w ago";
        if (days < 365) return $"{(int)(days / 30)}mo ago";
        return $"{(int)(days / 365)}y ago";
    }
}
