using ReType.Core.Config;
using ReType.Core.Converters;
using ReType.Core.Jobs;

namespace ReType.Core.Tests;

public class JobQueueTests : IDisposable
{
    private readonly string tempDir;

    public JobQueueTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "retype-job-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, true); } catch { }
    }

    private sealed class StubConverter : IConverter
    {
        public string Name => "Stub";

        public bool CanConvert(FileFamily source, FileFamily target) =>
            source == FileFamily.Image && target == FileFamily.Image;

        public async Task<ConversionResult> ConvertAsync(
            string inputPath, string outputPath, Settings settings, CancellationToken ct)
        {
            var bytes = await File.ReadAllBytesAsync(inputPath, ct);
            // 「変換後」の目印として 1 バイト追加して書き出す。
            await File.WriteAllBytesAsync(outputPath, [.. bytes, 0xFF], ct);
            return ConversionResult.Ok(outputPath);
        }
    }

    private string CreateInput(string name, byte[] content)
    {
        var path = Path.Combine(tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public async Task Process_ConvertsAndKeepsOriginal_WhenKeepOriginal()
    {
        var settings = new Settings { KeepOriginal = true };
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        var job = new ConversionJob(Guid.NewGuid(), tempDir, "photo.png", "photo.jpg", FileFamily.Image, FileFamily.Image);
        CreateInput("photo.jpg", [1, 2, 3]);

        using var queue = new JobQueue(registry, settings, tracker);
        var tcs = new TaskCompletionSource<JobOutcome>();
        queue.Completed += o => tcs.TrySetResult(o);
        queue.Enqueue(job);

        var outcome = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(outcome.Success);
        Assert.Equal(job.InputPath, outcome.OutputPath);

        // 元内容は旧名で復元され、出力は変換されている。
        Assert.True(File.Exists(job.OriginalPath));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(job.OriginalPath));
        Assert.Equal([1, 2, 3, 0xFF], File.ReadAllBytes(job.InputPath));

        // 両パスともループ防止に登録されている。
        Assert.True(tracker.Contains(job.OriginalPath));
        Assert.True(tracker.Contains(job.InputPath));
    }

    [Fact]
    public async Task Process_OverwritesInPlace_WhenKeepOriginalDisabled()
    {
        var settings = new Settings { KeepOriginal = false };
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        var job = new ConversionJob(Guid.NewGuid(), tempDir, "photo.png", "photo.jpg", FileFamily.Image, FileFamily.Image);
        CreateInput("photo.jpg", [1, 2, 3]);

        using var queue = new JobQueue(registry, settings, tracker);
        var tcs = new TaskCompletionSource<JobOutcome>();
        queue.Completed += o => tcs.TrySetResult(o);
        queue.Enqueue(job);

        var outcome = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(outcome.Success);

        // 旧名ファイルは作られず、入力ファイルが変換内容で上書きされる。
        Assert.False(File.Exists(job.OriginalPath));
        Assert.Equal([1, 2, 3, 0xFF], File.ReadAllBytes(job.InputPath));
    }

    [Fact]
    public async Task Process_ReportsError_ForUnsupportedPair()
    {
        var settings = new Settings();
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        var job = new ConversionJob(Guid.NewGuid(), tempDir, "song.mp3", "song.wav", FileFamily.Audio, FileFamily.Audio);

        using var queue = new JobQueue(registry, settings, tracker);
        var tcs = new TaskCompletionSource<JobOutcome>();
        queue.Completed += o => tcs.TrySetResult(o);
        queue.Enqueue(job);

        var outcome = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(outcome.Success);
        Assert.Contains("サポートされていない", outcome.Error);
    }
}
