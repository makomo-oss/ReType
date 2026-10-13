using ReType.Core;

namespace ReType.Converters;

/// <summary>Converts documents (md/html/docx/epub/...) with pandoc.</summary>
public sealed class PandocConverter : IConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public bool CanConvert(string fromExt, string toExt) =>
        ConversionMatrix.IsSupported(fromExt, toExt) &&
        ConversionMatrix.CategoryOf(fromExt) == ConversionCategory.Document;

    public async Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var pandoc = ToolRunner.Locate("pandoc.exe")
            ?? throw new ConversionFailedException(
                "pandoc is not installed. Place pandoc.exe in thirdparty\\bin or add it to PATH.");

        var result = await ToolRunner.RunAsync(
            pandoc, $"\"{inputPath}\" -o \"{outputPath}\"", Timeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            var lastLine = result.StandardError
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .LastOrDefault();
            throw new ConversionFailedException(lastLine is null ? "pandoc failed" : $"pandoc: {lastLine}");
        }
    }
}
