namespace ReType.Core;

/// <summary>Progress tick reported by a long-running conversion. Percent is 0-100, or null when the total is unknown.</summary>
public sealed record ConversionProgress(double? Percent);

/// <summary>Performs the actual content conversion for one extension pair.</summary>
public interface IConverter
{
    /// <summary>True when this converter handles the (fromExt, toExt) pair. Extensions are normalized.</summary>
    bool CanConvert(string fromExt, string toExt);

    /// <summary>
    /// Converts the content of <paramref name="inputPath"/> (known to be of format
    /// <paramref name="fromExt"/>) and writes the result to <paramref name="outputPath"/>.
    /// </summary>
    Task ConvertAsync(string inputPath, string fromExt, string toExt, string outputPath, CancellationToken cancellationToken);

    /// <summary>
    /// Progress-reporting variant. The default implementation ignores progress;
    /// converters that can report (ffmpeg-based ones) override it.
    /// </summary>
    Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
        => ConvertAsync(inputPath, fromExt, toExt, outputPath, cancellationToken);
}
