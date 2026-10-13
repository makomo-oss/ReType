using ReType.Converters;
using ReType.Core;
using Xunit;

namespace ReType.Tests;

public sealed class FfmpegConverterTests : IDisposable
{
    private readonly string _dir;
    private readonly FfmpegConverter _converter = new();

    public FfmpegConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "retype-ffmpeg-" + Guid.NewGuid().ToString("N"));
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

    private static string? Ffmpeg => ToolRunner.Locate("ffmpeg.exe");

    [Fact]
    public async Task WavToMp3_ProducesMp3()
    {
        if (Ffmpeg is null)
        {
            return; // ffmpeg not installed on this machine; nothing to verify.
        }

        var wav = Path.Combine(_dir, "tone.wav");
        var mp3 = Path.Combine(_dir, "tone.mp3");
        await File.WriteAllBytesAsync(wav, BuildSilentWav(TimeSpan.FromMilliseconds(200)));

        await _converter.ConvertAsync(wav, ".wav", ".mp3", mp3, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(mp3);
        bool looksLikeMp3 = bytes.Length > 3 &&
            ((bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3') || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0));
        Assert.True(looksLikeMp3, "output does not look like an MP3 file");
    }

    [Fact]
    public async Task Mp4ToMkv_RemuxesLosslessly()
    {
        if (Ffmpeg is null)
        {
            return;
        }

        var mp4 = await CreateSampleMp4Async();
        var mkv = Path.Combine(_dir, "sample.mkv");

        await _converter.ConvertAsync(mp4, ".mp4", ".mkv", mkv, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(mkv);
        Assert.True(bytes.Length > 4 &&
            bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3,
            "output is not a Matroska container");
    }

    [Fact]
    public async Task Mp4ToWebm_ReEncodes()
    {
        if (Ffmpeg is null)
        {
            return;
        }

        var mp4 = await CreateSampleMp4Async();
        var webm = Path.Combine(_dir, "sample.webm");

        await _converter.ConvertAsync(mp4, ".mp4", ".webm", webm, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(webm);
        Assert.True(bytes.Length > 4 &&
            bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3,
            "output is not a Matroska/WebM container");
    }

    [Fact]
    public void CanConvert_RespectsMatrix()
    {
        Assert.True(_converter.CanConvert(".mp4", ".mkv"));
        Assert.True(_converter.CanConvert(".wav", ".flac"));
        Assert.False(_converter.CanConvert(".mp4", ".mp3"));
        Assert.False(_converter.CanConvert(".md", ".docx"));
    }

    private async Task<string> CreateSampleMp4Async()
    {
        var mp4 = Path.Combine(_dir, "sample.mp4");
        var result = await ToolRunner.RunAsync(
            Ffmpeg!,
            $"-hide_banner -loglevel error -y -f lavfi -i testsrc=duration=1:size=64x64:rate=10 -c:v libx264 \"{mp4}\"",
            TimeSpan.FromMinutes(2),
            CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        return mp4;
    }

    private static byte[] BuildSilentWav(TimeSpan duration)
    {
        const int sampleRate = 8000;
        const int bitsPerSample = 16;
        const int channels = 1;
        int sampleCount = (int)(sampleRate * duration.TotalSeconds);
        int dataSize = sampleCount * channels * bitsPerSample / 8;
        int headerSize = 44;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(headerSize + dataSize - 8);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
        return stream.ToArray();
    }
}
