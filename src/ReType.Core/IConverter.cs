namespace ReType.Core;

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
}
