using ReType.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ReType.Converters;

/// <summary>
/// Converts images using the built-in Windows imaging codecs
/// (Windows.Graphics.Imaging), so no external dependency is required.
/// WebP encoding is not covered by these codecs; see <see cref="WebPConverter"/>.
/// </summary>
public sealed class ImageConverter : IConverter
{
    private static readonly Dictionary<string, Guid> EncoderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = BitmapEncoder.JpegEncoderId,
        [".jpeg"] = BitmapEncoder.JpegEncoderId,
        [".png"] = BitmapEncoder.PngEncoderId,
        [".bmp"] = BitmapEncoder.BmpEncoderId,
        [".gif"] = BitmapEncoder.GifEncoderId,
        [".tif"] = BitmapEncoder.TiffEncoderId,
        [".tiff"] = BitmapEncoder.TiffEncoderId,
    };

    /// <summary>Target formats that cannot store transparency.</summary>
    private static readonly HashSet<string> OpaqueTargets =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".bmp" };

    public bool CanConvert(string fromExt, string toExt) =>
        ConversionMatrix.IsSupported(fromExt, toExt) && EncoderIds.ContainsKey(toExt);

    public async Task ConvertAsync(
        string inputPath,
        string fromExt,
        string toExt,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!EncoderIds.TryGetValue(toExt, out var encoderId))
        {
            throw new ConversionFailedException($"Cannot write image format {toExt}");
        }

        BitmapDecoder decoder;
        byte[] pixels;
        uint pixelWidth, pixelHeight;
        double dpiX, dpiY;
        using (var input = File.OpenRead(inputPath))
        using (var inputStream = input.AsRandomAccessStream())
        {
            try
            {
                decoder = await BitmapDecoder.CreateAsync(inputStream).AsTask(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ConversionFailedException(
                    "The image could not be decoded. The content may not match its extension, " +
                    "or a codec (e.g. HEIF/HEVC) is missing on this system.",
                    ex);
            }

            // The decoder reads lazily; extract pixels before the stream is disposed.
            var pixelProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);
            pixels = pixelProvider.DetachPixelData();
            pixelWidth = decoder.PixelWidth;
            pixelHeight = decoder.PixelHeight;
            dpiX = decoder.DpiX;
            dpiY = decoder.DpiY;
        }

        var alphaMode = OpaqueTargets.Contains(toExt) ? BitmapAlphaMode.Ignore : BitmapAlphaMode.Straight;

        using var output = File.Create(outputPath);
        using var outputStream = output.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream).AsTask(cancellationToken);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            alphaMode,
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            pixels);
        await encoder.FlushAsync().AsTask(cancellationToken);
    }
}
