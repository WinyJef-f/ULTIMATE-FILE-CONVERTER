using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UltimateFileConverter.WinUI.Engine;

/// <summary>
/// In-process subtitle text conversion between SubRip (SRT) and YouTube SBV. Faithful port
/// of the macOS <c>SubtitleConverter</c>.
///
/// FFmpeg handles SRT ⇄ ASS ⇄ VTT directly, but its SBV support is unreliable, so the router
/// bridges SBV through SRT using this converter (the <see cref="Tool.Subtitle"/> sentinel
/// step): SBV → SRT → {ass,vtt} and {ass,vtt} → SRT → SBV. The two formats differ only in
/// their cue framing and timestamp punctuation, so the bridge is a faithful, lossless
/// round-trip of timings and text.
///
/// SRT cue:
/// <code>
/// 1
/// 00:00:01,000 --> 00:00:04,000
/// First line
/// Second line
/// </code>
/// SBV cue:
/// <code>
/// 0:00:01.000,0:00:04.000
/// First line
/// Second line
/// </code>
/// </summary>
public static class SubtitleConverter
{
    /// <summary>One subtitle cue: a time range (in milliseconds) and its text lines.</summary>
    private sealed record Cue(int StartMs, int EndMs, IReadOnlyList<string> Lines);

    /// <summary>
    /// Dispatched from <see cref="ToolRunner"/>. <paramref name="arguments"/> is
    /// <c>[action, inputPath, outputPath]</c> where action is "sbv2srt" or "srt2sbv".
    /// </summary>
    public static void Run(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 3)
        {
            throw new System.ArgumentException("Subtitle step needs an action, input, and output path.");
        }

        var action = arguments[0];
        var input = arguments[1];
        var output = arguments[2];

        switch (action)
        {
            case "sbv2srt":
                System.IO.File.WriteAllText(output, BuildSrt(ParseSbv(ReadText(input))), new UTF8Encoding(false));
                break;
            case "srt2sbv":
                System.IO.File.WriteAllText(output, BuildSbv(ParseSrt(ReadText(input))), new UTF8Encoding(false));
                break;
            default:
                throw new System.ArgumentException($"Unknown subtitle action: {action}");
        }
    }

    // MARK: - Parsing

    /// <summary>Splits text into cue blocks separated by one or more blank lines.</summary>
    private static List<List<string>> Blocks(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var result = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in normalized.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Count > 0) { result.Add(current); current = new List<string>(); }
            }
            else
            {
                current.Add(line);
            }
        }
        if (current.Count > 0) result.Add(current);
        return result;
    }

    private static List<Cue> ParseSrt(string text)
    {
        var cues = new List<Cue>();
        foreach (var block in Blocks(text))
        {
            var timingIndex = block.FindIndex(l => l.Contains("-->"));
            if (timingIndex < 0) continue;
            var parts = block[timingIndex].Split("-->");
            if (parts.Length != 2) continue;
            var start = ParseTimestamp(parts[0]);
            var end = ParseTimestamp(parts[1]);
            if (start is null || end is null) continue;
            var lines = block.Skip(timingIndex + 1).ToList();
            cues.Add(new Cue(start.Value, end.Value, lines));
        }
        return cues;
    }

    private static List<Cue> ParseSbv(string text)
    {
        var cues = new List<Cue>();
        foreach (var block in Blocks(text))
        {
            if (block.Count == 0) continue;
            var parts = block[0].Split(',');
            if (parts.Length != 2) continue;
            var start = ParseTimestamp(parts[0]);
            var end = ParseTimestamp(parts[1]);
            if (start is null || end is null) continue;
            var lines = block.Skip(1).ToList();
            cues.Add(new Cue(start.Value, end.Value, lines));
        }
        return cues;
    }

    /// <summary>Parses <c>H:MM:SS.mmm</c> or <c>HH:MM:SS,mmm</c> (either punctuation) into milliseconds.</summary>
    private static int? ParseTimestamp(string raw)
    {
        var trimmed = raw.Trim().Replace(',', '.');
        var timeParts = trimmed.Split(':');
        if (timeParts.Length != 3) return null;
        var secondsParts = timeParts[2].Split('.');
        if (!int.TryParse(timeParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)) return null;
        if (!int.TryParse(timeParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)) return null;
        if (!int.TryParse(secondsParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)) return null;
        var millis = 0;
        if (secondsParts.Length > 1)
        {
            int.TryParse(PadRight(secondsParts[1], 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out millis);
        }
        return ((hours * 60 + minutes) * 60 + seconds) * 1000 + millis;
    }

    // MARK: - Building

    private static string BuildSrt(IReadOnlyList<Cue> cues)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < cues.Count; i++)
        {
            sb.Append(i + 1).Append('\n');
            sb.Append(FormatSrt(cues[i].StartMs)).Append(" --> ").Append(FormatSrt(cues[i].EndMs)).Append('\n');
            sb.Append(string.Join("\n", cues[i].Lines));
            sb.Append("\n\n");
        }
        return sb.ToString();
    }

    private static string BuildSbv(IReadOnlyList<Cue> cues)
    {
        var sb = new StringBuilder();
        foreach (var cue in cues)
        {
            sb.Append(FormatSbv(cue.StartMs)).Append(',').Append(FormatSbv(cue.EndMs)).Append('\n');
            sb.Append(string.Join("\n", cue.Lines));
            sb.Append("\n\n");
        }
        return sb.ToString();
    }

    /// <summary><c>HH:MM:SS,mmm</c> (SubRip).</summary>
    private static string FormatSrt(int ms)
    {
        var (h, m, s, millis) = Split(ms);
        return $"{h:D2}:{m:D2}:{s:D2},{millis:D3}";
    }

    /// <summary><c>H:MM:SS.mmm</c> (YouTube SBV — hours are not zero-padded).</summary>
    private static string FormatSbv(int ms)
    {
        var (h, m, s, millis) = Split(ms);
        return $"{h}:{m:D2}:{s:D2}.{millis:D3}";
    }

    private static (int H, int M, int S, int Millis) Split(int ms)
    {
        var clamped = ms < 0 ? 0 : ms;
        return (clamped / 3_600_000, clamped / 60_000 % 60, clamped / 1000 % 60, clamped % 1000);
    }

    // MARK: - Helpers

    /// <summary>Right-pads a fractional-seconds string with zeros so e.g. "5" → "500" (centiseconds → ms).</summary>
    private static string PadRight(string value, int length) =>
        value.Length >= length ? value[..length] : value.PadRight(length, '0');

    private static string ReadText(string path)
    {
        // UTF-8 with BOM detection; falls back to lossy decoding for odd encodings.
        return System.IO.File.ReadAllText(path);
    }
}
