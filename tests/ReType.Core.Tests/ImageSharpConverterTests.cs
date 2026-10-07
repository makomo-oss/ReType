using ReType.Core.Config;
using ReType.Core.Converters;
using ReType.Core.Tools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReType.Core.Tests;

public class ImageSharpConverterTests : IDisposable
{
    private readonly string dir;
    private readonly ImageSharpConverter converter = new();
    private readonly Settings settings = new();

    public ImageSharpConverterTests()
    {
        dir = Path.Combine(Path.GetTempPath(), "retype-test-img-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
    }

    public void Dispose() => Directory.Delete(dir, true);

    /// <summary>単色のテスト画像を生成してパスを返す。</summary>
    private string CreateImage(string extension)
    {
        var path = Path.Combine(dir, "input" + extension);
        using var image = new Image<Rgb24>(4, 4, new Rgb24(255, 0, 0));
        image.Save(path);
        return path;
    }

    [Fact]
    public void CanConvert_OnlyImageToImage()
    {
        Assert.True(converter.CanConvert(FileFamily.Image, FileFamily.Image));
        Assert.False(converter.CanConvert(FileFamily.Image, FileFamily.Document));
        Assert.False(converter.CanConvert(FileFamily.Audio, FileFamily.Image));
    }

    [Theory]
    [InlineData(".png", ".jpg")]
    [InlineData(".jpg", ".png")]
    [InlineData(".png", ".webp")]
    [InlineData(".bmp", ".png")]
    public async Task Convert_ProducesValidImage(string sourceExt, string targetExt)
    {
        var input = CreateImage(sourceExt);
        var output = Path.Combine(dir, "output" + targetExt);

        var result = await converter.ConvertAsync(input, output, settings, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(output, result.OutputPath);
        Assert.True(File.Exists(output));

        using var loaded = Image.Load(output);
        Assert.Equal(4, loaded.Width);
        Assert.Equal(4, loaded.Height);
    }

    [Fact]
    public async Task Convert_Avif_ViaFfmpeg_WhenAvailable()
    {
        // ImageSharp には AVIF エンコーダがないため ffmpeg に委譲される。
        // ffmpeg がインストールされていない環境ではスキップ。
        var resolver = new ExternalToolResolver();
        if (resolver.Find("ffmpeg") is null)
            return;

        var converter = new ImageSharpConverter(resolver);
        var input = CreateImage(".png");
        var output = Path.Combine(dir, "output.avif");

        var result = await converter.ConvertAsync(input, output, settings, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(output));

        // ImageSharp 3.1.5 には AVIF デコーダがないため、ftyp ボックスで検証する。
        var header = File.ReadAllBytes(output)[..12];
        Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(header[4..8]));
        Assert.Equal("avif", System.Text.Encoding.ASCII.GetString(header[8..12]));
    }

    [Fact]
    public async Task Convert_Fails_OnInvalidInput()
    {
        var input = Path.Combine(dir, "broken.png");
        File.WriteAllText(input, "not an image");
        var output = Path.Combine(dir, "out.jpg");

        var result = await converter.ConvertAsync(input, output, settings, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ImageSharp", result.Error);
        Assert.False(File.Exists(output));
    }
}
