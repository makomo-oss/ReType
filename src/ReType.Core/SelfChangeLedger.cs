using System.Collections.Concurrent;

namespace ReType.Core;

/// <summary>
/// Tracks file paths the app itself created or renamed so that the file system
/// watcher can ignore the resulting events and avoid conversion loops.
/// </summary>
public sealed class SelfChangeLedger
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _ttl;

    public SelfChangeLedger(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromSeconds(10);
    }

    public void Record(string path)
    {
        _entries[Normalize(path)] = DateTimeOffset.UtcNow;
        RemoveExpired();
    }

    public bool Contains(string path)
    {
        if (!_entries.TryGetValue(Normalize(path), out var timestamp))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - timestamp > _ttl)
        {
            _entries.TryRemove(Normalize(path), out _);
            return false;
        }

        return true;
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (path, timestamp) in _entries)
        {
            if (now - timestamp > _ttl)
            {
                _entries.TryRemove(path, out _);
            }
        }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
