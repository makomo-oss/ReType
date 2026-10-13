using ReType.Converters;
using ReType.Core;
using ReType.Core.Settings;

namespace ReType.App;

/// <summary>
/// Tray-resident host: watches the configured folders and runs conversions
/// when the user changes a file extension in Explorer.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly SettingsService _settingsService = new();
    private readonly SelfChangeLedger _ledger = new();
    private readonly RenameDetector _detector = new();
    private readonly NotifyIcon _notifyIcon;
    private FileWatcherService? _watcher;
    private ConversionQueue? _queue;
    private AppSettings _settings;

    public TrayContext()
    {
        _settings = _settingsService.Load();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "ReType",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("監視フォルダ...", null, (_, _) => ShowFoldersForm());

        var autoStartItem = new ToolStripMenuItem("Windows 起動時に開始")
        {
            Checked = AutoStart.IsEnabled,
        };
        autoStartItem.Click += (_, _) =>
        {
            AutoStart.SetEnabled(!autoStartItem.Checked);
            autoStartItem.Checked = AutoStart.IsEnabled;
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowFoldersForm();

        RestartWatching();
    }

    private void ShowFoldersForm()
    {
        using var form = new FoldersForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.ResultSettings;
            _settingsService.Save(_settings);
            RestartWatching();
        }
    }

    private void RestartWatching()
    {
        _watcher?.Dispose();
        _queue?.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var reporter = new BalloonReporter(_notifyIcon);
        var service = new ConversionService(CreateConverters(_settings), _ledger, reporter);
        _queue = new ConversionQueue(service.ProcessRenameAsync);
        _watcher = new FileWatcherService(_ledger);
        _watcher.ExternalRenameDetected += (oldPath, newPath) => _queue.Enqueue(_detector.Detect(oldPath, newPath));
        _watcher.Watch(_settings.WatchedFolders, _settings.IncludeSubdirectories);
    }

    private static IReadOnlyList<IConverter> CreateConverters(AppSettings settings)
    {
        var converters = new List<IConverter>();
        if (settings.EnableImageConversions)
        {
            converters.Add(new ReType.Converters.ImageConverter());
            converters.Add(new WebPConverter());
        }

        if (settings.EnableVideoConversions || settings.EnableAudioConversions)
        {
            converters.Add(new FfmpegConverter());
        }

        if (settings.EnableDocumentConversions)
        {
            converters.Add(new PandocConverter());
        }

        return converters;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _watcher?.Dispose();
            _queue?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}
