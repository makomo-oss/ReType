using ReType.Core.Settings;

namespace ReType.App;

/// <summary>Dialog for editing watched folders and conversion categories.</summary>
public sealed class FoldersForm : Form
{
    private readonly ListBox _folderList = new()
    {
        Location = new Point(12, 12),
        Size = new Size(420, 180),
        IntegralHeight = false,
    };
    private readonly CheckBox _subdirectories = new()
    {
        Text = "サブフォルダも監視する",
        Location = new Point(12, 200),
        AutoSize = true,
    };
    private readonly CheckBox _images = new() { Text = "画像", Location = new Point(12, 232), AutoSize = true };
    private readonly CheckBox _video = new() { Text = "動画", Location = new Point(84, 232), AutoSize = true };
    private readonly CheckBox _audio = new() { Text = "音声", Location = new Point(156, 232), AutoSize = true };
    private readonly CheckBox _documents = new() { Text = "文書", Location = new Point(228, 232), AutoSize = true };
    private readonly Label _categoriesLabel = new()
    {
        Text = "変換カテゴリ:",
        Location = new Point(12, 258),
        AutoSize = true,
    };

    public AppSettings ResultSettings { get; private set; }

    public FoldersForm(AppSettings settings)
    {
        ResultSettings = settings;

        Text = "ReType 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 320);

        var addButton = new Button { Text = "追加...", Location = new Point(438, 12), Size = new Size(110, 28) };
        addButton.Click += OnAddFolder;
        var removeButton = new Button { Text = "削除", Location = new Point(438, 44), Size = new Size(110, 28) };
        removeButton.Click += (_, _) =>
        {
            if (_folderList.SelectedIndex >= 0)
            {
                _folderList.Items.RemoveAt(_folderList.SelectedIndex);
            }
        };

        foreach (var folder in settings.WatchedFolders)
        {
            _folderList.Items.Add(folder);
        }

        _subdirectories.Checked = settings.IncludeSubdirectories;
        _images.Checked = settings.EnableImageConversions;
        _video.Checked = settings.EnableVideoConversions;
        _audio.Checked = settings.EnableAudioConversions;
        _documents.Checked = settings.EnableDocumentConversions;

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(352, 286), Size = new Size(90, 28) };
        okButton.Click += OnOk;
        var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(450, 286), Size = new Size(90, 28) };

        Controls.AddRange([
            _folderList, addButton, removeButton,
            _subdirectories, _categoriesLabel, _images, _video, _audio, _documents,
            okButton, cancelButton,
        ]);
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
    }
}
