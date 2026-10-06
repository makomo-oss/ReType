using ReType.Core.Config;
using ReType.Core.Converters;
using ReType.Core.Jobs;
using ReType.Core.Watching;

namespace ReType.Core.Tests;

public class DirectoryWatcherTests : IDisposable
{
    private readonly string tempDir;

    public DirectoryWatcherTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "retype-watcher-tests-" + Guid.NewGuid());
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

        public Task<ConversionResult> ConvertAsync(
            string inputPath, string outputPath, Settings settings, CancellationToken ct)
        {
            File.WriteAllBytes(outputPath, [0xFF]);
            return Task.FromResult(ConversionResult.Ok(outputPath));
        }
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(tempDir, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private static async Task<ConversionJob?> WaitForEvent(
        TaskCompletionSource<ConversionJob> tcs, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        return completed == tcs.Task ? await tcs.Task : null;
    }

    [Fact]
    public async Task ExtensionChange_EnqueuesJob()
    {
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        using var queue = new JobQueue(registry, new Settings { KeepOriginal = false }, tracker);
        using var watcher = new DirectoryWatcher(tempDir, tracker, queue);

        var tcs = new TaskCompletionSource<ConversionJob>();
        watcher.Enqueued += job => tcs.TrySetResult(job);
        watcher.Start();

        var input = CreateFile("photo.png");
        var renamed = Path.Combine(tempDir, "photo.jpg");
        File.Move(input, renamed);

        var job = await WaitForEvent(tcs, TimeSpan.FromSeconds(5));
        Assert.NotNull(job);
        Assert.Equal("photo.png", job.OriginalName);
        Assert.Equal("photo.jpg", job.NewName);
        Assert.Equal(FileFamily.Image, job.SourceFamily);
        Assert.Equal(FileFamily.Image, job.TargetFamily);
    }

    [Fact]
    public async Task SameExtensionRename_IsIgnored()
    {
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        using var queue = new JobQueue(registry, new Settings(), tracker);
        using var watcher = new DirectoryWatcher(tempDir, tracker, queue);

        var tcs = new TaskCompletionSource<ConversionJob>();
        watcher.Enqueued += job => tcs.TrySetResult(job);
        watcher.UnsupportedRename += msg => tcs.TrySetException(new Exception(msg));
        watcher.Start();

        var input = CreateFile("b.png");
        File.Move(input, Path.Combine(tempDir, "b2.png"));

        var job = await WaitForEvent(tcs, TimeSpan.FromSeconds(1.5));
        Assert.Null(job);
    }

    [Fact]
    public async Task UnsupportedExtension_ReportsError()
    {
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        using var queue = new JobQueue(registry, new Settings(), tracker);
        using var watcher = new DirectoryWatcher(tempDir, tracker, queue);

        var tcs = new TaskCompletionSource<string>();
        watcher.UnsupportedRename += msg => tcs.TrySetResult(msg);
        watcher.Start();

        var input = CreateFile("archive.png");
        File.Move(input, Path.Combine(tempDir, "archive.zip"));

        var message = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("サポートされていない", message);
    }

    [Fact]
    public async Task SelfWrite_IsIgnored()
    {
        var tracker = new SelfWriteTracker();
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter());

        using var queue = new JobQueue(registry, new Settings(), tracker);
        using var watcher = new DirectoryWatcher(tempDir, tracker, queue);

        var tcs = new TaskCompletionSource<ConversionJob>();
        watcher.Enqueued += job => tcs.TrySetResult(job);
        watcher.Start();

        // アプリ自身の操作として登録してからリネーム → 検知されない。
        var input = CreateFile("d.png");
        var renamed = Path.Combine(tempDir, "d.jpg");
        tracker.Add(input);
        tracker.Add(renamed);
        File.Move(input, renamed);

        var job = await WaitForEvent(tcs, TimeSpan.FromSeconds(1.5));
        Assert.Null(job);
    }
}
