using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace UltimateFileConverter.WinUI.Services;

/// <summary>Checks GitHub Releases for a version newer than the current build.</summary>
public static class UpdateChecker
{
    public const string CurrentVersion = "1.0.2";
    private const string ApiUrl = "https://api.github.com/repos/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest";

    public sealed record CheckResult(bool IsUpdateAvailable, string? TagName, string? HtmlUrl);

    public static async Task<CheckResult> CheckAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "ULTIMATE-FILE-CONVERTER");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.Timeout = System.TimeSpan.FromSeconds(10);

        var json = await client.GetStringAsync(ApiUrl);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.GetProperty("tag_name").GetString();
        var htmlUrl = root.GetProperty("html_url").GetString();
        if (tagName is null) throw new System.Exception("Could not parse GitHub response.");

        return new CheckResult(IsNewer(tagName, CurrentVersion), tagName, htmlUrl);
    }

    private static bool IsNewer(string tag, string current)
    {
        var clean = tag.StartsWith('v') ? tag[1..] : tag;
        return CompareVersions(clean, current) > 0;
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.'); var pb = b.Split('.');
        var count = System.Math.Max(pa.Length, pb.Length);
        for (var i = 0; i < count; i++)
        {
            var va = i < pa.Length && int.TryParse(pa[i], out var n1) ? n1 : 0;
            var vb = i < pb.Length && int.TryParse(pb[i], out var n2) ? n2 : 0;
            if (va != vb) return va > vb ? 1 : -1;
        }
        return 0;
    }
}
