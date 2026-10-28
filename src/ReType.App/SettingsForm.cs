using ReType.Core;
using ReType.Core.Settings;
using ReType.Core.Updates;

namespace ReType.App;

/// <summary>ReType settings: watched folders, categories, autostart, version and updates.</summary>
public sealed class SettingsForm : Form
{
    private readonly ListBox _folderList = new()
    {
        Location = new Point(12, 22),
        Size = new Size(400, 150),
        IntegralHeight = false,
    };
    private readonly CheckBox _subdirectories = new()
    {
        Text = "サブフォルダも監視する",
        Location = new Point(12, 180),
        AutoSize = true,
    };
    private readonly CheckBox _images = new() { Text = "画像", Location = new Point(14, 26), AutoSize = true };
    private readonly CheckBox _video = new() { Text = "動画", Location = new Point(86, 26), AutoSize = true };
    private readonly CheckBox _audio = new() { Text = "音声", Location = new Point(158, 26), AutoSize = true };
    private readonly CheckBox _documents = new() { Text = "文書", Location = new Point(230, 26), AutoSize = true };
    private readonly CheckBox _autoStart = new()
    {
        Text = "Windows の起動時に ReType を自動的に開始する",
        Location = new Point(14, 24),
        AutoSize = true,
    };
    private readonly Label _versionLabel = new() { Location = new Point(14, 22), AutoSize = true };
    private readonly Button _checkUpdateButton = new()
    {
        Text = "更新を確認",
        Location = new Point(14, 50),
        Size = new Size(110, 28),
    };
    private readonly Label _updateStatus = new()
    {
        Location = new Point(136, 55),
        AutoSize = true,
    };
    private readonly Button _updateButton = new()
    {
        Location = new Point(394, 50),
        Size = new Size(110, 28),
        Visible = false,
    };
    private readonly ListView _historyList = new()
    {
        Location = new Point(12, 22),
        Size = new Size(400, 116),
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
    };
    private readonly Button _undoButton = new()
    {
        Text = "元に戻す",
        Location = new Point(414, 22),
        Size = new Size(90, 28),
    };
    private readonly Button _clearHistoryButton = new()
    {
        Text = "履歴を消去",
        Location = new Point(394, 54),
        Size = new Size(110, 28),
    };
    private readonly Label _historyStatus = new()
    {
        Location = new Point(12, 144),
        AutoSize = true,
    };

    private readonly Func<Task<ReleaseInfo?>> _checkForUpdates;
    private readonly Func<ReleaseInfo, Task> _applyUpdate;
    private readonly Version _currentVersion;
    private readonly ConversionHistoryStore? _history;
    private readonly Func<ConversionEntry, Task<string?>>? _undoHandler;
    private ReleaseInfo? _availableUpdate;

    public AppSettings ResultSettings { get; private set; }
    public bool ResultAutoStart { get; private set; }

    public SettingsForm(
        AppSettings settings,
        bool autoStartEnabled,
        Version currentVersion,
        Func<Task<ReleaseInfo?>> checkForUpdates,
        Func<ReleaseInfo, Task> applyUpdate,
        ConversionHistoryStore? history = null,
        Func<ConversionEntry, Task<string?>>? undoHandler = null)
    {
        ResultSettings = settings;
        ResultAutoStart = autoStartEnabled;
        _currentVersion = currentVersion;
        _checkForUpdates = checkForUpdates;
        _applyUpdate = applyUpdate;
        _history = history;
        _undoHandler = undoHandler;

        Text = "ReType 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        // FixedDialogs are hidden from the taskbar by default; keep the button
        // so the window is findable even if activation ever fails.
        ShowInTaskbar = true;
        ClientSize = new Size(540, 544);

        // --- Watched folders ---
        var foldersGroup = new GroupBox { Text = "監視フォルダ", Location = new Point(12, 8), Size = new Size(516, 210) };
        foreach (var folder in settings.WatchedFolders)
        {
            _folderList.Items.Add(folder);
        }

        var addButton = new Button { Text = "追加...", Location = new Point(420, 22), Size = new Size(86, 28) };
        addButton.Click += OnAddFolder;
        var removeButton = new Button { Text = "削除", Location = new Point(420, 54), Size = new Size(86, 28) };
        removeButton.Click += (_, _) =>
        {
            if (_folderList.SelectedIndex >= 0)
            {
                _folderList.Items.RemoveAt(_folderList.SelectedIndex);
            }
        };

        _subdirectories.Checked = settings.IncludeSubdirectories;
        foldersGroup.Controls.AddRange([_folderList, addButton, removeButton, _subdirectories]);

        // --- Categories ---
        var categoriesGroup = new GroupBox { Text = "変換カテゴリ", Location = new Point(12, 224), Size = new Size(516, 150) };
        _images.Checked = settings.EnableImageConversions;
        _video.Checked = settings.EnableVideoConversions;
        _audio.Checked = settings.EnableAudioConversions;
        _documents.Checked = settings.EnableDocumentConversions;
        categoriesGroup.Controls.AddRange([_images, _video, _audio, _documents]);

        // Show what each category actually accepts, generated from the same
        // table the detector consults so the UI can never drift from it.
        categoriesGroup.Controls.AddRange([
            new Label { Text = "画像: " + FormatFormats(ConversionMatrix.ImageFormats), Location = new Point(14, 50), AutoSize = true },
            new Label { Text = "動画: " + FormatFormats(ConversionMatrix.VideoFormats), Location = new Point(14, 66), AutoSize = true },
            new Label { Text = "音声: " + FormatFormats(ConversionMatrix.AudioFormats), Location = new Point(14, 82), AutoSize = true },
            new Label { Text = "文書: " + FormatFormats(ConversionMatrix.DocumentFormats), Location = new Point(14, 98), AutoSize = true },
            new Label
            {
                Text = "カテゴリをまたぐ変換（画像→音声など）は未対応です",
                Location = new Point(14, 116),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
            },
        ]);

        // --- Startup ---
        var startupGroup = new GroupBox { Text = "起動", Location = new Point(12, 380), Size = new Size(516, 50) };
        _autoStart.Checked = autoStartEnabled;
        startupGroup.Controls.Add(_autoStart);

        // --- Version and updates ---
        var updateGroup = new GroupBox { Text = "バージョンとアップデート", Location = new Point(12, 436), Size = new Size(516, 92) };
        _versionLabel.Text = $"現在のバージョン: {_currentVersion}";
        _checkUpdateButton.Click += OnCheckUpdateClick;
        _updateButton.Click += OnUpdateClick;
        updateGroup.Controls.AddRange([_versionLabel, _checkUpdateButton, _updateStatus, _updateButton]);

        // --- Conversion history (optional; hidden for hosts without a store) ---
        Control? historyGroup = null;
        if (_history is not null)
        {
            historyGroup = new GroupBox { Text = "最近の変換", Location = new Point(12, 534), Size = new Size(516, 170) };
            _historyList.Columns.Add("日時", 110);
            _historyList.Columns.Add("ファイル", 190);
            _historyList.Columns.Add("結果", 80);
            PopulateHistory();
            _undoButton.Click += OnUndoClick;
            _clearHistoryButton.Click += (_, _) =>
            {
                _history.Clear();
                _historyList.Items.Clear();
                _historyStatus.Text = "履歴を消去しました";
            };
            historyGroup.Controls.AddRange([_historyList, _undoButton, _clearHistoryButton, _historyStatus]);
        }

        var formHeight = historyGroup is null ? 570 : 746;
        ClientSize = new Size(540, formHeight);

        // --- Dialog buttons ---
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(332, formHeight - 36), Size = new Size(90, 28) };
        okButton.Click += OnOk;
        var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(430, formHeight - 36), Size = new Size(90, 28) };
        // DialogResult only auto-closes forms shown via ShowDialog; this form is
        // modeless, so close it ourselves. Button.OnClick has already set
        // Form.DialogResult by the time the Click event fires, so TrayContext can
        // still tell OK from Cancel/X in FormClosed.
        cancelButton.Click += (_, _) => Close();

        Controls.AddRange(historyGroup is null
            ? [foldersGroup, categoriesGroup, startupGroup, updateGroup, okButton, cancelButton]
            : [foldersGroup, categoriesGroup, startupGroup, updateGroup, historyGroup, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void PopulateHistory()
    {
        _historyList.Items.Clear();
        foreach (var entry in _history!.Load())
        {
            var item = new ListViewItem(entry.Timestamp.ToString("MM-dd HH:mm"));
            item.SubItems.Add(Path.GetFileName(entry.NewPath));
            item.SubItems.Add(entry.Outcome switch
            {
                ConversionOutcome.Succeeded => "完了",
                ConversionOutcome.Failed => "失敗",
                ConversionOutcome.Unsupported => "未対応",
                ConversionOutcome.Undone => "元に戻した",
                _ => entry.Outcome.ToString(),
            });
            if (entry.Reason is { Length: > 0 } reason)
            {
                item.ToolTipText = reason;
            }

            item.Tag = entry;
            _historyList.Items.Add(item);
        }
    }

    private async void OnUndoClick(object? sender, EventArgs e)
    {
        if (_undoHandler is null || _historyList.SelectedItems.Count == 0)
        {
            return;
        }

        var entry = (ConversionEntry)_historyList.SelectedItems[0].Tag!;
        _undoButton.Enabled = false;
        _clearHistoryButton.Enabled = false;
        _historyStatus.Text = "元に戻しています...";
        try
        {
            var error = await _undoHandler(entry);
            _historyStatus.Text = error ?? "元に戻しました（ロスレスでない形式は再エンコードされています）";
            PopulateHistory();
        }
        catch (Exception ex)
        {
            _historyStatus.Text = $"元に戻せませんでした: {ex.Message}";
        }
        finally
        {
            _undoButton.Enabled = true;
            _clearHistoryButton.Enabled = true;
        }
    }

    private static string FormatFormats(IReadOnlyList<string> extensions) =>
        string.Join(" / ", extensions.Select(e => e.TrimStart('.')));

    private void OnAddFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK &&
            !_folderList.Items.Contains(dialog.SelectedPath))
        {
            _folderList.Items.Add(dialog.SelectedPath);
        }
    }

    private void OnOk(object? sender, EventArgs e)
    {
        ResultSettings = ResultSettings with
        {
            WatchedFolders = _folderList.Items.Cast<string>().ToList(),
            IncludeSubdirectories = _subdirectories.Checked,
            EnableImageConversions = _images.Checked,
            EnableVideoConversions = _video.Checked,
            EnableAudioConversions = _audio.Checked,
            EnableDocumentConversions = _documents.Checked,
        };
        ResultAutoStart = _autoStart.Checked;
        Close();
    }

    private async void OnCheckUpdateClick(object? sender, EventArgs e)
    {
        _checkUpdateButton.Enabled = false;
        _updateButton.Visible = false;
        _availableUpdate = null;
        _updateStatus.Text = "確認中...";
        try
        {
            var release = await _checkForUpdates();
            if (release?.Version is null)
            {
                _updateStatus.Text = "公開されているリリースが見つかりません";
            }
            else if (release.Version > _currentVersion)
            {
                _availableUpdate = release;
                _updateStatus.Text = $"新しいバージョン {release.Version} が利用できます";
                _updateButton.Text = $"{release.Version} に更新";
                _updateButton.Visible = true;
            }
            else
            {
                _updateStatus.Text = "最新です";
            }
        }
        catch (Exception ex)
        {
            _updateStatus.Text = $"確認に失敗: {ex.Message}";
        }
        finally
        {
            _checkUpdateButton.Enabled = true;
        }
    }

    private async void OnUpdateClick(object? sender, EventArgs e)
    {
        var release = _availableUpdate;
        if (release is null)
        {
            return;
        }

        _checkUpdateButton.Enabled = false;
        _updateButton.Enabled = false;
        _updateStatus.Text = "インストーラーをダウンロードしています...";
        try
        {
            await _applyUpdate(release);
            // The app exits once the update is handed off; the form closes with it.
        }
        catch (Exception ex)
        {
            _updateStatus.Text = $"更新に失敗: {ex.Message}";
            _checkUpdateButton.Enabled = true;
            _updateButton.Enabled = true;
        }
    }
}
