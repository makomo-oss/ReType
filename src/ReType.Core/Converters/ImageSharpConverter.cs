using System.Diagnostics;
using ReType.Core.Config;
using ReType.Core.Tools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace ReType.Core.Converters;

/// <summary>
/// 画像→画像の変換エンジン（SixLabors.ImageSharp）。
/// Image→Document（PDF）は DocumentConverter（QuestPDF）が担当する。
/// ImageSharp は AVIF をエンコードできないため、AVIF 出力のみ ffmpeg に委譲する。
/// </summary>
public sealed class ImageSharpConverter : IConverter
{
    private readonly ExternalToolResolver resolver;

    public ImageSharpConverter(ExternalToolResolver? resolver = null)
    {
        this.resolver = resolver ?? new ExternalToolResolver();
    }

    public string Name => "ImageSharp";

    public bool CanConvert(FileFamily source, FileFamily target) =>
        source == FileFamily.Image && target == FileFamily.Image;

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        Settings settings,
        CancellationToken ct)
    {
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        if (ext == ".avif")
            return await ConvertViaFfmpegAsync(inputPath, outputPath, settings, ct);

        try
        {
            using var image = Image.Load(inputPath);
            var encoder = PickEncoder(ext, settings.Quality.ImageQuality);
            await image.SaveAsync(outputPath, encoder, ct);
            return ConversionResult.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return ConversionResult.Fail($"ImageSharp: {ex.Message}");
        }
    }

    private static IImageEncoder PickEncoder(string ext, int quality)
    {
        quality = Math.Clamp(quality, 0, 100);
        return ext switch
        {
            ".jpg" or ".jpeg" => new JpegEncoder { Quality = quality },
            ".webp" => new WebpEncoder { Quality = quality },
            ".png" => new PngEncoder(),
            ".gif" => new GifEncoder(),
            ".bmp" => new BmpEncoder(),
            ".tif" or ".tiff" => new TiffEncoder(),
            _ => new PngEncoder(),
        };
    }

    /// <summary>
    /// AVIF 出力を ffmpeg でエンコードする（ImageSharp に AVIF エンコーダがないため）。
    /// ユーザー品質 0-100 を libaom の CRF 63-0 に線形変換する。
    /// </summary>
    private async Task<ConversionResult> ConvertViaFfmpegAsync(
        string inputPath,
        string outputPath,
        Settings settings,
        CancellationToken ct)
    {
        var ffmpeg = resolver.Find("ffmpeg");
        if (ffmpeg is null)
            return ConversionResult.Fail(
                "ImageSharp: AVIF encoding requires ffmpeg. Install ffmpeg or set Settings.ToolsDirectory.");

        int crf = (100 - Math.Clamp(settings.Quality.ImageQuality, 0, 100)) * 63 / 100;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var args = new List<string>
        {
            "-y", "-loglevel", "error", "-i", inputPath,
            "-c:v", "libaom-av1", "-crf", crf.ToString(), outputPath,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi)!;
            string stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
                return ConversionResult.Fail($"ffmpeg: {stderr.Trim()}");
            return ConversionResult.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return ConversionResult.Fail($"ffmpeg: {ex.Message}");
        }
    }
}
