using System.Text.RegularExpressions;
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

    public Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        CancellationToken cancellationToken)
        => ConvertAsync(inputPath, fromExt, toExt, outputPath, null, cancellationToken);

    public async Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ffmpeg = ToolRunner.Locate("ffmpeg.exe")
            ?? throw new ConversionFailedException(
                "ffmpeg is not installed. Place ffmpeg.exe in thirdparty\\bin or add it to PATH.");

        var isVideo = ConversionMatrix.CategoryOf(fromExt) == ConversionCategory.Video;

        // Percentages need the total duration; probe it once (cheap: ffmpeg -i
        // without an output exits immediately after printing the stream info).
        var duration = progress is null ? null : await ProbeDurationAsync(ffmpeg, inputPath, cancellationToken);
        var progressLines = CreateProgressLines(progress, duration);
        var progressArgs = progress is null ? string.Empty : " -progress pipe:1";

        if (isVideo)
        {
            var remux = await ToolRunner.RunAsync(
                ffmpeg, Args(inputPath, outputPath, "-c copy") + progressArgs, RemuxTimeout, cancellationToken, progressLines);
            if (remux.ExitCode == 0)
            {
                return;
            }

            // Streams are not compatible with the target container; re-encode with defaults.
            var encode = await ToolRunner.RunAsync(
                ffmpeg, Args(inputPath, outputPath, EncodeArgs(toExt)) + progressArgs, EncodeTimeout, cancellationToken, progressLines);
            if (encode.ExitCode != 0)
            {
                throw new ConversionFailedException(DescribeFailure(encode.StandardError));
            }

            return;
        }

        var audio = await ToolRunner.RunAsync(
            ffmpeg, Args(inputPath, outputPath) + progressArgs, EncodeTimeout, cancellationToken, progressLines);
        if (audio.ExitCode != 0)
        {
            throw new ConversionFailedException(DescribeFailure(audio.StandardError));
        }
    }

    /// <summary>Turns ffmpeg's machine-readable progress lines into ConversionProgress ticks.</summary>
    private static IProgress<string>? CreateProgressLines(IProgress<ConversionProgress>? progress, TimeSpan? duration)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<string>(line =>
        {
            if (!IsProgressLine(line))
            {
                return;
            }

            // Without a known total, still report that work is advancing (null percent).
            progress.Report(new ConversionProgress(TryGetProgressPercent(line, duration ?? TimeSpan.Zero)));
        });
    }

    private static async Task<TimeSpan?> ProbeDurationAsync(string ffmpeg, string inputPath, CancellationToken cancellationToken)
    {
        try
        {
            var probe = await ToolRunner.RunAsync(
                ffmpeg, $"-hide_banner -i \"{inputPath}\"", TimeSpan.FromSeconds(10), cancellationToken);
            return TryGetDuration(probe.StandardError);
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Parses the "Duration: 00:12:34.56" line from ffmpeg's stream info output.</summary>
    public static TimeSpan? TryGetDuration(string ffmpegOutput)
    {
        var match = Regex.Match(ffmpegOutput, @"Duration:\s*(\d+):(\d+):(\d+)\.(\d+)");
        if (!match.Success)
        {
            return null;
        }

        var hours = int.Parse(match.Groups[1].Value);
        var minutes = int.Parse(match.Groups[2].Value);
        var seconds = int.Parse(match.Groups[3].Value);
        var fraction = match.Groups[4].Value;
        // ffmpeg prints hundredths ("00:12:34.56"); be tolerant of other widths.
        var milliseconds = fraction.Length switch
        {
            1 => int.Parse(fraction) * 100,
            2 => int.Parse(fraction) * 10,
            _ => int.Parse(fraction[..3]),
        };

        return new TimeSpan(hours, minutes, seconds) + TimeSpan.FromMilliseconds(milliseconds);
    }

    private static bool IsProgressLine(string line) => line.StartsWith("out_time_ms=", StringComparison.Ordinal);

    /// <summary>
    /// Computes 0-100 from an ffmpeg "-progress pipe:1" out_time_ms line.
    /// Despite the name, out_time_ms is in microseconds.
    /// </summary>
    public static double? TryGetProgressPercent(string line, TimeSpan duration)
    {
        if (!IsProgressLine(line) || duration.Ticks <= 0)
        {
            return null;
        }

        if (!long.TryParse(line.AsSpan("out_time_ms=".Length), out var microseconds) || microseconds < 0)
        {
            return null;
        }

        var done = TimeSpan.FromTicks(microseconds * 10); // 1 microsecond = 10 ticks
        return Math.Clamp(done.TotalMilliseconds / duration.TotalMilliseconds * 100.0, 0, 100);
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
