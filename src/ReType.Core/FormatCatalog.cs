namespace ReType.Core;

/// <summary>
/// 拡張子と FileFamily の対応表。README の対応フォーマットが唯一の真実。
/// </summary>
public static class FormatCatalog
{
    private static readonly Dictionary<string, FileFamily> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 画像
        ["png"] = FileFamily.Image,
        ["jpg"] = FileFamily.Image,
        ["jpeg"] = FileFamily.Image,
        ["webp"] = FileFamily.Image,
        ["gif"] = FileFamily.Image,
        ["bmp"] = FileFamily.Image,
        ["tiff"] = FileFamily.Image,
        ["tif"] = FileFamily.Image,
        ["avif"] = FileFamily.Image,

        // 動画
        ["mp4"] = FileFamily.Video,
        ["webm"] = FileFamily.Video,
        ["avi"] = FileFamily.Video,
        ["mov"] = FileFamily.Video,
        ["mkv"] = FileFamily.Video,

        // 音声
        ["mp3"] = FileFamily.Audio,
        ["wav"] = FileFamily.Audio,
        ["flac"] = FileFamily.Audio,
        ["ogg"] = FileFamily.Audio,
        ["m4a"] = FileFamily.Audio,

        // ドキュメント
        ["md"] = FileFamily.Document,
        ["txt"] = FileFamily.Document,
        ["html"] = FileFamily.Document,
        ["htm"] = FileFamily.Document,
        ["rtf"] = FileFamily.Document,
        ["pdf"] = FileFamily.Document,
    };

    /// <summary>拡張子（".png" でも "png" でも可）からファミリーを判定する。</summary>
    public static FileFamily GetFamily(string extension)
    {
        var key = extension.StartsWith('.') ? extension[1..] : extension;
        return Map.TryGetValue(key, out var family) ? family : FileFamily.Unknown;
    }

    /// <summary>指定ファミリーの正規拡張子一覧（GUI のターゲット選択用）。</summary>
    public static IReadOnlyList<string> ExtensionsOf(FileFamily family) =>
        Map.Where(kv => kv.Value == family).Select(kv => "." + kv.Key).OrderBy(e => e).ToList();
}
