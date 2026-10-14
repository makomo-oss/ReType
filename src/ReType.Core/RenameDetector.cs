namespace ReType.Core;

public enum RenameDecisionKind
{
    /// <summary>Plain rename (same or aliased extension); nothing to convert.</summary>
    Ignore,

    /// <summary>The extension changed to a supported target; convert the content.</summary>
    Convert,

    /// <summary>The extension changed but ReType cannot perform this conversion.</summary>
    Unsupported,
}

/// <summary>Result of inspecting a rename event against the conversion matrix.</summary>
public sealed record RenameDecision(
    RenameDecisionKind Kind,
    string OldPath,
    string NewPath,
    string FromExtension,
    string ToExtension)
{
    public static RenameDecision Ignore(string oldPath, string newPath) =>
        new(RenameDecisionKind.Ignore, oldPath, newPath, string.Empty, string.Empty);
}

/// <summary>
/// Decides whether a file rename should trigger a content conversion.
/// Handles the hidden-extension case: with extensions hidden in Explorer,
/// renaming "photo.jpg" to "photo.webp" actually produces "photo.webp.jpg".
/// Renames that involve transient (download/temp/backup) extensions, or whose
/// source format is unknown, are program operations rather than user
/// conversion requests and are always ignored.
/// </summary>
public sealed class RenameDetector
{
    /// <summary>Extensions used by downloads, atomic saves, and backups; never conversion requests.</summary>
    private static readonly HashSet<string> TransientExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".part", ".partial", ".crdownload", ".download",
        ".opdownload", ".dzip", ".bak", ".old", ".etl", ".wip", ".wbk",
    };

    public RenameDecision Detect(string oldPath, string newPath)
    {
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return RenameDecision.Ignore(oldPath, newPath);
        }

        var oldExt = ConversionMatrix.Normalize(Path.GetExtension(oldPath));
        var newExt = ConversionMatrix.Normalize(Path.GetExtension(newPath));

        // Browsers finish downloads as "file.exe.tmp -> file.exe"; editors and
        // installers use similar temp renames. Never treat those as conversions.
        if (TransientExtensions.Contains(oldExt) || TransientExtensions.Contains(newExt))
        {
            return RenameDecision.Ignore(oldPath, newPath);
        }

        if (newExt == oldExt)
        {
            // "photo.jpg" -> "photo.webp.jpg": the requested extension is hidden before the real one.
            var stem = Path.GetFileNameWithoutExtension(newPath);
            var stemExt = ConversionMatrix.Normalize(Path.GetExtension(stem));
            if (stemExt.Length > 0 && ConversionMatrix.IsSupported(oldExt, stemExt))
            {
                return new RenameDecision(RenameDecisionKind.Convert, oldPath, newPath, oldExt, stemExt);
            }

            return RenameDecision.Ignore(oldPath, newPath);
        }

        if (ConversionMatrix.IsSameFormat(oldExt, newExt))
        {
            return RenameDecision.Ignore(oldPath, newPath);
        }

        if (ConversionMatrix.IsSupported(oldExt, newExt))
        {
            return new RenameDecision(RenameDecisionKind.Convert, oldPath, newPath, oldExt, newExt);
        }

        // Only report unsupported (and roll back) when both extensions are formats
        // ReType knows about, e.g. .jpg -> .mp3. An unknown target extension
        // (.xyz, .exe, ...) is left untouched.
        if (ConversionMatrix.CategoryOf(oldExt) is not null && ConversionMatrix.CategoryOf(newExt) is not null)
        {
            return new RenameDecision(RenameDecisionKind.Unsupported, oldPath, newPath, oldExt, newExt);
        }

        return RenameDecision.Ignore(oldPath, newPath);
    }
}
