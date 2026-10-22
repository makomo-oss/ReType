using ReType.Core;
using Xunit;

namespace ReType.Tests;

public sealed class ConversionServiceTests : IDisposable
{
    private readonly string _dir;

    public ConversionServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "retype-tests-" + Guid.NewGuid().ToString("N"));
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

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task SupportedRename_ReplacesFileWithConvertedContent()
    {
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.webp");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: true, produce: "CONVERTED"));
        var decision = new RenameDetector().Detect(oldPath, newPath);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.True(File.Exists(newPath));
        Assert.Equal("CONVERTED", File.ReadAllText(newPath));
        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public async Task FailedConversion_RestoresOriginalName()
    {
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.webp");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: true, fail: true));
        var decision = new RenameDetector().Detect(oldPath, newPath);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        // The renamed file is rolled back to its original name with its original content.
        Assert.True(File.Exists(oldPath));
        Assert.Equal("ORIGINAL", File.ReadAllText(oldPath));
        Assert.False(File.Exists(newPath));
    }

    [Fact]
    public async Task NoConverterRegistered_RestoresOriginalName()
    {
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.webp");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: false, produce: "x"));
        var decision = new RenameDetector().Detect(oldPath, newPath);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.True(File.Exists(oldPath));
        Assert.False(File.Exists(newPath));
    }

    [Fact]
    public async Task UnsupportedRename_RestoresOriginalName()
    {
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.mp3");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: true, produce: "x"));
        var decision = new RenameDetector().Detect(oldPath, newPath);
        Assert.Equal(RenameDecisionKind.Unsupported, decision.Kind);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.True(File.Exists(oldPath));
        Assert.False(File.Exists(newPath));
    }

    [Fact]
    public async Task HiddenExtensionRename_ReplacesFileWithConvertedContent()
    {
        // "photo.jpg" -> "photo.webp.jpg" (hidden-extension case).
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.webp.jpg");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: true, produce: "CONVERTED"));
        var decision = new RenameDetector().Detect(oldPath, newPath);
        Assert.Equal(RenameDecisionKind.Convert, decision.Kind);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.True(File.Exists(newPath));
        Assert.Equal("CONVERTED", File.ReadAllText(newPath));
    }

    [Fact]
    public async Task HiddenExtensionUnsupportedRename_RestoresOriginalName()
    {
        // Extensions hidden: "song.jpg" renamed to "song.mp3" yields song.mp3.jpg on disk.
        var oldPath = CreateFile("song.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "song.mp3.jpg");
        File.Move(oldPath, newPath);

        var service = MakeService(new FakeConverter(canConvert: true, produce: "x"));
        var decision = new RenameDetector().Detect(oldPath, newPath);
        Assert.Equal(RenameDecisionKind.Unsupported, decision.Kind);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.True(File.Exists(oldPath));
        Assert.False(File.Exists(newPath));
    }

    [Fact]
    public async Task FileVanishedBeforeProcessing_ReportsNotFound()
    {
        var oldPath = CreateFile("photo.jpg", "ORIGINAL");
        var newPath = Path.Combine(_dir, "photo.webp");
        File.Move(oldPath, newPath);
        File.Delete(newPath); // vanished before the queue got to it

        var reporter = new RecordingReporter();
        var service = new ConversionService(
            [new FakeConverter(canConvert: true, produce: "x")],
            new SelfChangeLedger(),
            reporter,
            TimeSpan.FromMilliseconds(200));
        var decision = new RenameDetector().Detect(oldPath, newPath);

        await service.ProcessRenameAsync(decision, CancellationToken.None);

        Assert.Contains(reporter.Failures, f => f.Reason == "ファイルが見つかりません");
    }

    private static ConversionService MakeService(IConverter converter) =>
        new([converter], new SelfChangeLedger(), NullConversionReporter.Instance, TimeSpan.FromMilliseconds(500));

    private sealed class FakeConverter : IConverter
    {
        private readonly bool _canConvert;
        private readonly string? _produce;
        private readonly bool _fail;

        public FakeConverter(bool canConvert, string? produce = null, bool fail = false)
        {
            _canConvert = canConvert;
            _produce = produce;
            _fail = fail;
        }

        public bool CanConvert(string fromExt, string toExt) =>
            _canConvert && fromExt == ".jpg" && toExt == ".webp";

        public Task ConvertAsync(string inputPath, string fromExt, string toExt, string outputPath, CancellationToken cancellationToken)
        {
            if (_fail)
            {
                throw new ConversionFailedException("simulated failure");
            }

            File.WriteAllText(outputPath, _produce ?? "OUTPUT");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingReporter : IConversionReporter
    {
        public List<(string Reason, bool RolledBack)> Failures { get; } = [];

        public void ConversionStarted(RenameDecision decision)
        {
        }

        public void ConversionSucceeded(RenameDecision decision, string outputPath)
        {
        }

        public void ConversionFailed(RenameDecision decision, string reason, bool rolledBack) =>
            Failures.Add((reason, rolledBack));

        public void ConversionUnsupported(RenameDecision decision)
        {
        }
    }
}
