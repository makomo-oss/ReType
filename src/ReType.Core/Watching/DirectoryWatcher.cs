using ReType.Core.Jobs;

namespace ReType.Core.Watching;

/// <summary>
/// 監視フォルダでのリネームを検知し、拡張子が変わったときだけジョブを投入する。
/// アプリ自身の操作（SelfWriteTracker）と同一拡張子のリネームは無視する。
/// サポート外の拡張子への変更はエラー通知のみ（リネームの巻き戻しはしない）。
/// </summary>
public sealed class DirectoryWatcher : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly SelfWriteTracker tracker;
    private readonly JobQueue queue;

    /// <summary>ジョブが投入されたときに呼ばれる（GUI のログ表示用）。</summary>
    public event Action<ConversionJob>? Enqueued;

    /// <summary>サポート外の拡張子への変更を検知したときに呼ばれる。</summary>
    public event Action<string>? UnsupportedRename;

    public DirectoryWatcher(string directory, SelfWriteTracker tracker, JobQueue queue)
    {
        WatchDirectory = directory;
        this.tracker = tracker;
        this.queue = queue;

        watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = false,
        };
        watcher.Renamed += OnRenamed;
    }

    public string WatchDirectory { get; }

    public void Start() => watcher.EnableRaisingEvents = true;

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (e.OldName is null || e.Name is null)
            return;

        // アプリ自身のリネーム（KeepOriginal による旧名復元など）は無視。
        if (tracker.Contains(e.FullPath) || tracker.Contains(e.OldFullPath))
            return;

        var oldExt = Path.GetExtension(e.OldName);
        var newExt = Path.GetExtension(e.Name);
        if (string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase))
            return;

        var source = FormatCatalog.GetFamily(oldExt);
        var target = FormatCatalog.GetFamily(newExt);
        if (source == FileFamily.Unknown || target == FileFamily.Unknown)
        {
            UnsupportedRename?.Invoke($"サポートされていない拡張子: {e.OldName} → {e.Name}");
            return;
        }

        var job = new ConversionJob(Guid.NewGuid(), WatchDirectory, e.OldName, e.Name, source, target);
        Enqueued?.Invoke(job);
        queue.Enqueue(job);
    }

    public void Dispose() => watcher.Dispose();
}
