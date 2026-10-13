using ReType.Core;

namespace ReType.Converters;

/// <summary>
/// Encodes WebP images with ffmpeg's libwebp encoder. The built-in Windows
/// codecs can decode WebP but cannot encode it, so encoding is routed here.
/// </summary>
public sealed class WebPConverter : IConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public bool CanConvert(string fromExt, string toExt) =>
        ConversionMatrix.IsSupported(fromExt, toExt) &&
        ConversionMatrix.CategoryOf(fromExt) == ConversionCategory.Image &&
        string.Equals(toExt, ".webp", StringComparison.OrdinalIgnoreCase);

    public async Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var ffmpeg = ToolRunner.Locate("ffmpeg.exe")
            ?? throw new ConversionFailedException(
                "ffmpeg is not installed. WebP encoding requires ffmpeg.exe in thirdparty\\bin or on PATH.");

        var result = await ToolRunner.RunAsync(
            ffmpeg,
            $"-hide_banner -loglevel error -y -i \"{inputPath}\" -c:v libwebp \"{outputPath}\"",
            Timeout,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            var lastLine = result.StandardError
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .LastOrDefault();
            throw new ConversionFailedException(lastLine is null ? "ffmpeg failed" : $"ffmpeg: {lastLine}");
        }
    }
}
