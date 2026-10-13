using ReType.Converters;
using ReType.Core;
using Xunit;

namespace ReType.Tests;

public sealed class ImageConverterTests : IDisposable
{
    // Canonical 1x1 opaque red pixel PNG.
    private static readonly byte[] SamplePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _dir;
    private readonly ImageConverter _converter = new();

    public ImageConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "retype-img-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".bmp")]
    [InlineData(".png")]
    [InlineData(".tif")]
    public async Task PngToTarget_ProducesValidFileWithMagicBytes(string toExt)
    {
        var input = Path.Combine(_dir, "sample.png");
        var output = Path.Combine(_dir, "sample" + toExt);
        await File.WriteAllBytesAsync(input, SamplePng);

        await _converter.ConvertAsync(input, ".png", toExt, output, CancellationToken.None);

        Assert.True(File.Exists(output));
        var bytes = await File.ReadAllBytesAsync(output);
        Assert.NotEmpty(bytes);
        AssertMagicBytes(output, bytes);
    }

    [Fact]
    public async Task RoundTrip_PngToJpegToPng_DecodesSuccessfully()
    {
        var png = Path.Combine(_dir, "sample.png");
        var jpeg = Path.Combine(_dir, "sample.jpg");
        var pngAgain = Path.Combine(_dir, "sample-again.png");
        await File.WriteAllBytesAsync(png, SamplePng);

        await _converter.ConvertAsync(png, ".png", ".jpg", jpeg, CancellationToken.None);
        await _converter.ConvertAsync(jpeg, ".jpg", ".png", pngAgain, CancellationToken.None);

        // The re-encoded PNG must still be decodable by the Windows codec.
        using var fileStream = File.OpenRead(pngAgain);
        using var stream = fileStream.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        Assert.Equal(1u, decoder.PixelWidth);
        Assert.Equal(1u, decoder.PixelHeight);
    }

    [Fact]
    public async Task CorruptInput_ThrowsConversionFailed()
    {
        var input = Path.Combine(_dir, "broken.png");
        var output = Path.Combine(_dir, "broken.jpg");
        await File.WriteAllTextAsync(input, "this is not a png");

        var ex = await Assert.ThrowsAsync<ConversionFailedException>(
            () => _converter.ConvertAsync(input, ".png", ".jpg", output, CancellationToken.None));
        Assert.Contains("decoded", ex.Message);
    }

    [Fact]
    public void CanConvert_RespectsMatrix()
    {
        Assert.True(_converter.CanConvert(".png", ".jpg"));
        Assert.False(_converter.CanConvert(".png", ".webp")); // handled by WebPConverter
        Assert.False(_converter.CanConvert(".mp3", ".wav"));
        Assert.False(_converter.CanConvert(".png", ".xyz"));
    }

    [Fact]
    public async Task PngToWebp_ProducesWebp()
    {
        if (ReType.Converters.ToolRunner.Locate("ffmpeg.exe") is null)
        {
            return; // ffmpeg not installed on this machine; nothing to verify.
        }

        var input = Path.Combine(_dir, "sample.png");
        var output = Path.Combine(_dir, "sample.webp");
        await File.WriteAllBytesAsync(input, SamplePng);

        await new WebPConverter().ConvertAsync(input, ".png", ".webp", output, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(output);
        AssertMagicBytes(output, bytes);
    }

    private static void AssertMagicBytes(string path, byte[] bytes)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        bool ok = ext switch
        {
            ".jpg" => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50,
            ".bmp" => bytes.Length > 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M',
            ".tif" => bytes.Length > 4 && bytes[0] == (byte)'I' && bytes[1] == (byte)'I',
            ".webp" => bytes.Length > 12
                && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false,
        };
        Assert.True(ok, $"{path} has unexpected magic bytes");
    }
}
