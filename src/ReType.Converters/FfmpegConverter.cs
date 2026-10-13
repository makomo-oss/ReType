using ReType.Core;

namespace ReType.Converters;

/// <summary>
/// Converts audio and video with ffmpeg. Video container changes (mp4/mkv/mov/...)
/// are first attempted as a lossless remux (-c copy) and fall back to a default
/// re-encode profile when the streams are not compatible with the target container.
/// </summary>
public sealed class FfmpegConverter : IConverter
{
    private static readonly TimeSpan RemuxTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EncodeTimeout = TimeSpan.FromMinutes(30);

    public bool CanConvert(string fromExt, string toExt)
    {
        if (!ConversionMatrix.IsSupported(fromExt, toExt))
        {
            return false;
        }

        var category = ConversionMatrix.CategoryOf(fromExt);
        return category is ConversionCategory.Video or ConversionCategory.Audio;
    }

    public async Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var ffmpeg = ToolRunner.Locate("ffmpeg.exe")
            ?? throw new ConversionFailedException(
                "ffmpeg is not installed. Place ffmpeg.exe in thirdparty\\bin or add it to PATH.");

        var isVideo = ConversionMatrix.CategoryOf(fromExt) == ConversionCategory.Video;

        if (isVideo)
        {
            var remux = await ToolRunner.RunAsync(
                ffmpeg, Args(inputPath, outputPath, "-c copy"), RemuxTimeout, cancellationToken);
            if (remux.ExitCode == 0)
            {
                return;
            }

            // Streams are not compatible with the target container; re-encode with defaults.
            var encode = await ToolRunner.RunAsync(
                ffmpeg, Args(inputPath, outputPath, EncodeArgs(toExt)), EncodeTimeout, cancellationToken);
            if (encode.ExitCode != 0)
            {
                throw new ConversionFailedException(DescribeFailure(encode.StandardError));
            }

            return;
        }

        var audio = await ToolRunner.RunAsync(ffmpeg, Args(inputPath, outputPath), EncodeTimeout, cancellationToken);
        if (audio.ExitCode != 0)
        {
            throw new ConversionFailedException(DescribeFailure(audio.StandardError));
        }
    }

    private static string Args(string inputPath, string outputPath, string extraArguments = "") =>
        $"-hide_banner -loglevel error -y -i \"{inputPath}\" {extraArguments} \"{outputPath}\"";

    private static string EncodeArgs(string toExt) => toExt.ToLowerInvariant() switch
    {
        ".webm" => "-c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus",
        _ => "-c:v libx264 -preset veryfast -crf 23 -c:a aac",
    };

    private static string DescribeFailure(string stderr)
    {
        var lastLine = stderr
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .LastOrDefault();
        return lastLine is null ? "ffmpeg failed" : $"ffmpeg: {lastLine}";
    }
}
