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
    // Captured lazily on the UI thread: the WinForms sync context is only
    // installed once the message loop starts, i.e. after this constructor runs.
    private SynchronizationContext? _syncContext;
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
        menu.Items.Add("設定...", null, (_, _) => ShowSettings());

        _updateMenuItem = new ToolStripMenuItem { Visible = false };
        _updateMenuItem.Click += OnUpdateClick;
        menu.Items.Add(_updateMenuItem);

        menu.Items.Add("更新を確認...", null, (_, _) => CheckForUpdatesInBackground(manual: true));

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
        _notifyIcon.Click += (_, _) => ShowSettings();
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        RestartWatching();

        // Background check shortly after startup; silent when up to date. Deferred
        // via a one-shot timer so it runs once the message loop (and with it the
        // WinForms sync context) is up.
        var startupUpdateCheck = new System.Windows.Forms.Timer { Interval = 1500 };
        startupUpdateCheck.Tick += (_, _) =>
        {
            startupUpdateCheck.Dispose();
            CheckForUpdatesInBackground(manual: false);
        };
        startupUpdateCheck.Start();
    }

    private SettingsForm? _settingsForm;

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            RaiseAboveTray(_settingsForm);
            return;
        }

        // Modeless on purpose: the tray stays responsive while settings are
        // open, and settings are applied on FormClosed.
        var form = new SettingsForm(
            _settings,
            AutoStart.IsEnabled,
            CurrentVersion,
            () => _updateChecker.GetLatestReleaseAsync(),
            ApplyUpdateAsync);
        _settingsForm = form;
        form.FormClosed += (_, _) =>
        {
            _settingsForm = null;
            if (form.DialogResult == DialogResult.OK)
            {
                _settings = form.ResultSettings;
                _settingsService.Save(_settings);
                AutoStart.SetEnabled(form.ResultAutoStart);
                RestartWatching();
            }

            form.Dispose();
        };
        form.Show();
        RaiseAboveTray(form);
    }

    /// <summary>
    /// Raises the window above everything and gives it focus. A tray app is
    /// never the foreground process (the shell receives the tray click), so
    /// Windows' foreground lock silently blocks Activate()/SetForegroundWindow
    /// and the window opens invisibly behind other windows. Attaching our
    /// input queue to the foreground window's thread lifts the block.
    /// It is repeated shortly after because the shell reasserts its own
    /// z-order when the tray flyout closes.
    /// </summary>
    private static void RaiseAboveTray(Form form)
    {
        BringToForeground(form);

        var raise = new System.Windows.Forms.Timer { Interval = 300 };
        raise.Tick += (_, _) =>
        {
            raise.Stop();
            raise.Dispose();
            if (!form.IsDisposed)
            {
                BringToForeground(form);
            }
        };
        raise.Start();
    }

    private static void BringToForeground(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var ourThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ourThread
            && AttachThreadInput(ourThread, foregroundThread, true);
        try
        {
            BringWindowToTop(form.Handle);
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(ourThread, foregroundThread, false);
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static Version CurrentVersion =>
        typeof(TrayContext).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>
    /// Kicks off the update check off the UI thread. Must be called on the UI
    /// thread: the sync context used to marshal the result back is captured here,
    /// which only works while the message loop is running.
    /// </summary>
    private void CheckForUpdatesInBackground(bool manual)
    {
        _syncContext ??= SynchronizationContext.Current;
        _ = Task.Run(() => CheckForUpdatesAsync(manual));
    }

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

        try
        {
            await ApplyUpdateAsync(release);
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

    /// <summary>Downloads the release installer and hands it to the update helper; the app then exits.</summary>
    private async Task ApplyUpdateAsync(ReleaseInfo release)
    {
        var installerPath = Path.Combine(Path.GetTempPath(), release.AssetName);
        await _updateChecker.DownloadInstallerAsync(release, installerPath);
        UpdateApplier.ApplyAndRestart(installerPath, Environment.ProcessPath!);
        ExitThread();
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
