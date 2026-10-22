using System.Collections.Concurrent;
using ReType.Core;
using Xunit;

namespace ReType.Tests;

public class FileWatcherServiceTests : IDisposable
{
    private readonly string _dir;

    public FileWatcherServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "retype-watcher-tests-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void Paused_SuppressesRenameEvents_AndResumeDeliversThem()
    {
        using var watcher = new FileWatcherService(new SelfChangeLedger());
        var events = new BlockingCollection<(string Old, string New)>();
        watcher.ExternalRenameDetected += (oldPath, newPath) => events.Add((oldPath, newPath));
        watcher.Watch([_dir], includeSubdirectories: false);

        // Paused: renames must not raise events...
        watcher.Paused = true;
        var first = Create("a.jpg");
        File.Move(first, Path.Combine(_dir, "b.jpg"));
        Assert.False(events.TryTake(out _, TimeSpan.FromSeconds(1.5)));

        // ...and resuming delivers subsequent renames.
        watcher.Paused = false;
        File.Move(Path.Combine(_dir, "b.jpg"), Path.Combine(_dir, "c.jpg"));
        Assert.True(events.TryTake(out var raised, TimeSpan.FromSeconds(5)));
        Assert.EndsWith("c.jpg", raised.New);
    }

    private string Create(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }
}
