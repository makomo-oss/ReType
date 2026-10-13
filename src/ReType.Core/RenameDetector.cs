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
/// </summary>
public sealed class RenameDetector
{
    public RenameDecision Detect(string oldPath, string newPath)
    {
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return RenameDecision.Ignore(oldPath, newPath);
        }

        var oldExt = ConversionMatrix.Normalize(Path.GetExtension(oldPath));
        var newExt = ConversionMatrix.Normalize(Path.GetExtension(newPath));

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

        return new RenameDecision(RenameDecisionKind.Unsupported, oldPath, newPath, oldExt, newExt);
    }
}
