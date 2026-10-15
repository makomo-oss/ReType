using System.Text.Json;

namespace ReType.Core.Updates;

/// <summary>A GitHub release with the ReType installer asset attached.</summary>
public sealed record ReleaseInfo(string TagName, Version? Version, string DownloadUrl, string AssetName)
{
    /// <summary>Parses the GitHub releases API response; null when no installer asset exists.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() ?? string.Empty : string.Empty;
        if (!root.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            if (!name.StartsWith("ReType-Setup-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
            if (url.Length > 0)
            {
                return new ReleaseInfo(tagName, ParseVersion(tagName), url, name);
            }
        }

        return null;
    }

    /// <summary>Extracts the version from a tag like "v0.1.2"; null when unparseable.</summary>
    public static Version? ParseVersion(string tagName)
    {
        var text = tagName.StartsWith('v') ? tagName[1..] : tagName;
        return Version.TryParse(text, out var version) ? version : null;
    }
}
