namespace ReType.Core;

/// <summary>Receives user-visible status of conversion jobs.</summary>
public interface IConversionReporter
{
    void ConversionStarted(RenameDecision decision);

    void ConversionSucceeded(RenameDecision decision, string outputPath);

    /// <summary>The conversion failed; <paramref name="rolledBack"/> tells whether the original name was restored.</summary>
    void ConversionFailed(RenameDecision decision, string reason, bool rolledBack);

    void ConversionUnsupported(RenameDecision decision);

    /// <summary>
    /// Periodic progress tick for a running conversion; percent is null when
    /// the total duration is unknown. Default: ignored.
    /// </summary>
    void ConversionProgress(RenameDecision decision, double? percent)
    {
    }
}

/// <summary>No-op reporter for hosts that do not surface status.</summary>
public sealed class NullConversionReporter : IConversionReporter
{
    public static readonly NullConversionReporter Instance = new();

    public void ConversionStarted(RenameDecision decision)
    {
    }

    public void ConversionSucceeded(RenameDecision decision, string outputPath)
    {
    }

    public void ConversionFailed(RenameDecision decision, string reason, bool rolledBack)
    {
    }

    public void ConversionUnsupported(RenameDecision decision)
    {
    }
}
