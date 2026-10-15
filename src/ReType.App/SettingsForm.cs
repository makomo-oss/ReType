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
        Location = new Point(416, 50),
        Size = new Size(110, 28),
        Visible = false,
    };

    private readonly Func<Task<ReleaseInfo?>> _checkForUpdates;
    private readonly Func<ReleaseInfo, Task> _applyUpdate;
    private readonly Version _currentVersion;
    private ReleaseInfo? _availableUpdate;

    public AppSettings ResultSettings { get; private set; }
    public bool ResultAutoStart { get; private set; }

    public SettingsForm(
        AppSettings settings,
        bool autoStartEnabled,
        Version currentVersion,
        Func<Task<ReleaseInfo?>> checkForUpdates,
        Func<ReleaseInfo, Task> applyUpdate)
    {
        ResultSettings = settings;
        ResultAutoStart = autoStartEnabled;
        _currentVersion = currentVersion;
        _checkForUpdates = checkForUpdates;
        _applyUpdate = applyUpdate;

        Text = "ReType 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 464);

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
        var categoriesGroup = new GroupBox { Text = "変換カテゴリ", Location = new Point(12, 224), Size = new Size(516, 52) };
        _images.Checked = settings.EnableImageConversions;
        _video.Checked = settings.EnableVideoConversions;
        _audio.Checked = settings.EnableAudioConversions;
        _documents.Checked = settings.EnableDocumentConversions;
        categoriesGroup.Controls.AddRange([_images, _video, _audio, _documents]);

        // --- Startup ---
        var startupGroup = new GroupBox { Text = "起動", Location = new Point(12, 282), Size = new Size(516, 50) };
        _autoStart.Checked = autoStartEnabled;
        startupGroup.Controls.Add(_autoStart);

        // --- Version and updates ---
        var updateGroup = new GroupBox { Text = "バージョンとアップデート", Location = new Point(12, 338), Size = new Size(516, 92) };
        _versionLabel.Text = $"現在のバージョン: {_currentVersion}";
        _checkUpdateButton.Click += OnCheckUpdateClick;
        _updateButton.Click += OnUpdateClick;
        updateGroup.Controls.AddRange([_versionLabel, _checkUpdateButton, _updateStatus, _updateButton]);

        // --- Dialog buttons ---
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(332, 434), Size = new Size(90, 28) };
        okButton.Click += OnOk;
        var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(430, 434), Size = new Size(90, 28) };

        Controls.AddRange([foldersGroup, categoriesGroup, startupGroup, updateGroup, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

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
