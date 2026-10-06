using ReType.Core.Config;

namespace ReType.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string tempDir;

    public SettingsStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "retype-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var store = new SettingsStore(Path.Combine(tempDir, "settings.json"));
        var settings = store.Load();

        Assert.True(settings.KeepOriginal);
        Assert.Equal(90, settings.Quality.ImageQuality);
        Assert.Equal(192, settings.Quality.AudioBitrateKbps);
        Assert.Equal(23, settings.Quality.VideoCrf);
        Assert.Empty(settings.WatchedFolders);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var store = new SettingsStore(Path.Combine(tempDir, "settings.json"));

        var settings = new Settings
        {
            KeepOriginal = false,
            WatchedFolders = ["C:\\watched"],
        };
        settings.Quality.ImageQuality = 75;
        store.Save(settings);

        var loaded = store.Load();
        Assert.False(loaded.KeepOriginal);
        Assert.Equal(75, loaded.Quality.ImageQuality);
        Assert.Equal(["C:\\watched"], loaded.WatchedFolders);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenJsonCorrupted()
    {
        var path = Path.Combine(tempDir, "settings.json");
        File.WriteAllText(path, "{ not valid json");

        var store = new SettingsStore(path);
        var settings = store.Load();

        Assert.True(settings.KeepOriginal);
    }
}
