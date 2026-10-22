using ReType.Core;
using Xunit;

namespace ReType.Tests;

public class ConversionHistoryStoreTests : IDisposable
{
    private readonly string _path;
    private readonly ConversionHistoryStore _store;

    public ConversionHistoryStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "retype-history-" + Guid.NewGuid().ToString("N") + ".json");
        _store = new ConversionHistoryStore(_path);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    private static ConversionEntry Entry(int i) => new(
        DateTimeOffset.UtcNow.AddMinutes(-i),
        $@"C:\t\file{i}.jpg",
        $@"C:\t\file{i}.webp",
        ".jpg",
        ".webp",
        ConversionOutcome.Succeeded);

    [Fact]
    public void Add_RoundTripsNewestFirst()
    {
        _store.Add(Entry(1));
        _store.Add(Entry(2));

        var loaded = _store.Load();
        Assert.Equal(2, loaded.Count);
        Assert.Equal(@"C:\t\file2.webp", loaded[0].NewPath);
        Assert.Equal(ConversionOutcome.Succeeded, loaded[0].Outcome);
    }

    [Fact]
    public void Add_TrimsToMaxEntries()
    {
        for (var i = 0; i < ConversionHistoryStore.MaxEntries + 25; i++)
        {
            _store.Add(Entry(i));
        }

        Assert.Equal(ConversionHistoryStore.MaxEntries, _store.Load().Count);
    }

    [Fact]
    public void Load_CorruptFileFallsBackToEmpty()
    {
        File.WriteAllText(_path, "not json at all {");
        Assert.Empty(_store.Load());
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        _store.Add(Entry(1));
        _store.Clear();
        Assert.Empty(_store.Load());
    }
}
