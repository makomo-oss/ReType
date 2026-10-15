using ReType.Converters;
using ReType.Core;
using ReType.Core.Settings;
using ReType.Core.Updates;

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
    private readonly UpdateChecker _updateChecker = new();
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _updateMenuItem;
    private FileWatcherService? _watcher;
    private ConversionQueue? _queue;
    private AppSettings _settings;
    private ReleaseInfo? _availableUpdate;

    public TrayContext()
    {
        _settings = _settingsService.Load();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "ReType",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("監視フォルダ...", null, (_, _) => ShowFoldersForm());

        _updateMenuItem = new ToolStripMenuItem { Visible = false };
        _updateMenuItem.Click += OnUpdateClick;
        menu.Items.Add(_updateMenuItem);

        menu.Items.Add("更新を確認...", null, async (_, _) => await CheckForUpdatesAsync(manual: true));

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

        // Background check shortly after startup; silent when up to date.
        _ = Task.Run(() => CheckForUpdatesAsync(manual: false));
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

    private static Version CurrentVersion =>
        typeof(TrayContext).Assembly.GetName().Version ?? new Version(0, 0);

    private async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            var release = await _updateChecker.GetLatestReleaseAsync();
            var available = UpdateChecker.IsUpdateAvailable(release, CurrentVersion);

            _syncContext?.Post(_ =>
            {
                if (available)
                {
                    _availableUpdate = release!;
                    _updateMenuItem.Text = $"ReType {release!.Version} にアップデート";
                    _updateMenuItem.Visible = true;
                    _updateMenuItem.Enabled = true;
                    if (!manual)
                    {
                        _notifyIcon.ShowBalloonTip(
                            6000, "ReType: アップデート",
                            $"新しいバージョン {release.Version} が利用できます。トレイメニューからアップデートできます。",
                            ToolTipIcon.Info);
                    }
                }
                else if (manual)
                {
                    _notifyIcon.ShowBalloonTip(
                        4000, "ReType: アップデート",
                        $"最新です（バージョン {CurrentVersion}）",
                        ToolTipIcon.Info);
                }
            }, null);
        }
        catch (Exception ex) when (manual)
        {
            _syncContext?.Post(_ => _notifyIcon.ShowBalloonTip(
                6000, "ReType: アップデート確認失敗", ex.Message, ToolTipIcon.Warning), null);
        }
        catch
        {
            // Background checks fail silently.
        }
    }

    private async void OnUpdateClick(object? sender, EventArgs e)
    {
        var release = _availableUpdate;
        if (release is null)
        {
            return;
        }

        _updateMenuItem.Enabled = false;
        _notifyIcon.ShowBalloonTip(
            4000, "ReType: アップデート",
            $"{release.Version} のインストーラーをダウンロードしています...",
            ToolTipIcon.Info);

        var installerPath = Path.Combine(Path.GetTempPath(), release.AssetName);
        try
        {
            await _updateChecker.DownloadInstallerAsync(release, installerPath);
            UpdateApplier.ApplyAndRestart(installerPath, Environment.ProcessPath!);
            ExitThread();
        }
        catch (Exception ex)
        {
            _updateMenuItem.Enabled = true;
            _notifyIcon.ShowBalloonTip(
                6000, "ReType: アップデート失敗",
                $"ダウンロードに失敗しました: {ex.Message}",
                ToolTipIcon.Warning);
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

    private static Icon LoadTrayIcon()
    {
        var stream = typeof(TrayContext).Assembly.GetManifestResourceStream("app.ico");
        if (stream is not null)
        {
            using (stream)
            {
                return new Icon(stream, 16, 16);
            }
        }

        return SystemIcons.Application;
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
