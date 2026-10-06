using System.Collections.Concurrent;

namespace ReType.Core.Jobs;

/// <summary>
/// アプリ自身が書き込んだ／リネームしたパスを記録する。
/// DirectoryWatcher はこのパスに関するイベントを無視し、自己変換のループを防ぐ。
/// </summary>
public sealed class SelfWriteTracker
{
    private readonly ConcurrentDictionary<string, byte> paths = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string path) => paths.TryAdd(Normalize(path), 0);

    public bool Contains(string path) => paths.ContainsKey(Normalize(path));

    private static string Normalize(string path) => path.TrimEnd('\\', '/');
}
