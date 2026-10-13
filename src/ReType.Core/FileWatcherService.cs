namespace ReType.Core;

/// <summary>
/// Watches folders for renames and raises events for changes that were not
/// produced by ReType itself.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private readonly SelfChangeLedger _ledger;
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _disposed;

    public FileWatcherService(SelfChangeLedger ledger)
    {
        _ledger = ledger;
    }

    /// <summary>Raised with (oldPath, newPath) for every external rename.</summary>
    public event Action<string, string>? ExternalRenameDetected;

    public void Watch(IEnumerable<string> folders, bool includeSubdirectories)
    {
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName,
            };
            watcher.Renamed += (_, e) => OnRenamed(e.OldFullPath, e.FullPath);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    private void OnRenamed(string oldPath, string newPath)
    {
        if (_disposed)
        {
            return;
        }

        if (_ledger.Contains(oldPath) || _ledger.Contains(newPath))
        {
            return;
        }

        if (IsReparsePoint(newPath))
        {
            // OneDrive placeholders and other reparse points are left untouched.
            return;
        }

        ExternalRenameDetected?.Invoke(oldPath, newPath);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }
}
