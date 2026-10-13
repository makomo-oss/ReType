namespace ReType.Core;

/// <summary>The kind of file content a conversion operates on.</summary>
public enum ConversionCategory
{
    Image,
    Video,
    Audio,
    Document,
}

/// <summary>
/// The canonical table of extension pairs ReType can convert.
/// A conversion is supported only when both extensions belong to the same category
/// and are not merely aliases of the same format.
/// </summary>
public static class ConversionMatrix
{
    public static readonly IReadOnlyList<string> ImageFormats =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff"];

    public static readonly IReadOnlyList<string> VideoFormats =
        [".mp4", ".mkv", ".mov", ".webm", ".avi", ".ts"];

    public static readonly IReadOnlyList<string> AudioFormats =
        [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma"];

    public static readonly IReadOnlyList<string> DocumentFormats =
        [".md", ".html", ".htm", ".txt", ".docx", ".epub", ".rst", ".rtf"];

    /// <summary>Extension groups that denote the same format; renaming between them needs no conversion.</summary>
    private static readonly string[][] SameFormatGroups =
    [
        [".jpg", ".jpeg"],
        [".tif", ".tiff"],
        [".htm", ".html"],
    ];

    public static string Normalize(string extension) => extension.ToLowerInvariant();

    public static ConversionCategory? CategoryOf(string extension)
    {
        extension = Normalize(extension);
        if (ImageFormats.Contains(extension)) return ConversionCategory.Image;
        if (VideoFormats.Contains(extension)) return ConversionCategory.Video;
        if (AudioFormats.Contains(extension)) return ConversionCategory.Audio;
        if (DocumentFormats.Contains(extension)) return ConversionCategory.Document;
        return null;
    }

    public static bool IsSameFormat(string fromExt, string toExt)
    {
        fromExt = Normalize(fromExt);
        toExt = Normalize(toExt);
        return SameFormatGroups.Any(group => group.Contains(fromExt) && group.Contains(toExt));
    }

    /// <summary>True when a real content conversion between the extensions is supported.</summary>
    public static bool IsSupported(string fromExt, string toExt)
    {
        fromExt = Normalize(fromExt);
        toExt = Normalize(toExt);
        if (fromExt == toExt || IsSameFormat(fromExt, toExt))
        {
            return false;
        }

        var from = CategoryOf(fromExt);
        return from is not null && from == CategoryOf(toExt);
    }
}
