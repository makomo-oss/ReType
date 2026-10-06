using ReType.Core.Config;
using ReType.Core.Converters;
using ReType.Core.Jobs;

namespace ReType.Core.Tests;

public class ConverterRegistryTests
{
    private sealed class StubConverter : IConverter
    {
        private readonly HashSet<(FileFamily, FileFamily)> pairs;

        public StubConverter(string name, params (FileFamily, FileFamily)[] pairs)
        {
            Name = name;
            this.pairs = [.. pairs];
        }

        public string Name { get; }

        public bool CanConvert(FileFamily source, FileFamily target) => pairs.Contains((source, target));

        public Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, Settings settings, CancellationToken ct) =>
            Task.FromResult(ConversionResult.Ok(outputPath));
    }

    [Fact]
    public void Resolve_PicksConverterThatSupportsPair()
    {
        var registry = new ConverterRegistry();
        var image = new StubConverter("Image", (FileFamily.Image, FileFamily.Image));
        var media = new StubConverter("Media", (FileFamily.Video, FileFamily.Audio));
        registry.Register(image);
        registry.Register(media);

        Assert.Same(image, registry.Resolve(FileFamily.Image, FileFamily.Image));
        Assert.Same(media, registry.Resolve(FileFamily.Video, FileFamily.Audio));
    }

    [Fact]
    public void Resolve_ReturnsNull_ForUnsupportedPair()
    {
        var registry = new ConverterRegistry();
        registry.Register(new StubConverter("Image", (FileFamily.Image, FileFamily.Image)));

        Assert.Null(registry.Resolve(FileFamily.Audio, FileFamily.Video));
    }

    [Fact]
    public void Job_PathsAreBuiltFromDirectoryAndNames()
    {
        var job = new ConversionJob(Guid.NewGuid(), @"C:\watched", "photo.png", "photo.jpg", FileFamily.Image, FileFamily.Image);

        Assert.Equal(Path.Combine(@"C:\watched", "photo.jpg"), job.InputPath);
        Assert.Equal(Path.Combine(@"C:\watched", "photo.png"), job.OriginalPath);
    }
}
