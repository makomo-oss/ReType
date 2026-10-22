using ReType.Core;

namespace ReType.App;

/// <summary>
/// Small top-most drop target: drop files, pick a target extension, and the
/// files are converted in place (original replaced; history Undo can revert).
/// </summary>
public sealed class DropForm : Form
{
    private readonly Func<string, string, Task<string?>> _convert;
    private readonly List<string> _files = [];
    private readonly Label _instruction;
    private readonly ComboBox _targetCombo = new()
    {
        Location = new Point(70, 112),
        Size = new Size(160, 24),
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly Button _convertButton = new()
    {
        Text = "変換",
        Location = new Point(342, 110),
        Size = new Size(86, 28),
    };
    private readonly TextBox _results = new()
    {
        Location = new Point(12, 148),
        Size = new Size(416, 140),
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    public DropForm(Func<string, string, Task<string?>> convert)
    {
        _convert = convert;

        Text = "ReType: ドロップ変換";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        AllowDrop = true;
        ClientSize = new Size(440, 300);

        _instruction = new Label
        {
            Text = "ここにファイルをドロップ\n（画像・動画・音声・文書）",
            Location = new Point(12, 12),
            Size = new Size(416, 90),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true,
        };

        var targetLabel = new Label { Text = "変換先:", Location = new Point(12, 116), AutoSize = true };
        _convertButton.Click += OnConvertClick;

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _instruction.DragEnter += OnDragEnter;
        _instruction.DragDrop += OnDragDrop;

        Controls.AddRange([_instruction, targetLabel, _targetCombo, _convertButton, _results]);
    }

    private static bool HasFiles(DragEventArgs e) => e.Data.GetDataPresent(DataFormats.FileDrop);

    /// <summary>Inspection accessors for tests verifying SetFiles state.</summary>
    public IReadOnlyList<string> TargetFormatsForTest => _targetCombo.Items.Cast<string>().ToList();

    public bool ConvertEnabledForTest => _convertButton.Enabled;

    private void OnDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (!HasFiles(e))
        {
            return;
        }

        SetFiles(((string[])e.Data.GetData(DataFormats.FileDrop)!).Where(File.Exists).ToList());
    }

    /// <summary>Registers the dropped files and offers the target formats they all accept.</summary>
    public void SetFiles(IReadOnlyList<string> files)
    {
        _files.Clear();
        _files.AddRange(files.Where(f => ConversionMatrix.CategoryOf(Path.GetExtension(f)) is not null));

        if (_files.Count == 0)
        {
            _instruction.Text = "対応していないファイルです";
            _targetCombo.Items.Clear();
            _convertButton.Enabled = false;
            return;
        }

        var names = _files.Select(Path.GetFileName).Take(3).ToList();
        _instruction.Text = $"{_files.Count} 個のファイル\n{string.Join(", ", names)}{(_files.Count > 3 ? " ..." : string.Empty)}";
        _convertButton.Enabled = true;

        // Offer only formats every dropped file can convert to; mixed categories share nothing.
        _targetCombo.Items.Clear();
        var categories = _files
            .Select(f => ConversionMatrix.CategoryOf(Path.GetExtension(f)))
            .Distinct()
            .ToList();
        if (categories.Count == 1)
        {
            foreach (var format in FormatsFor(categories[0]!.Value))
            {
                if (_files.All(f => ConversionMatrix.IsSupported(ConversionMatrix.Normalize(Path.GetExtension(f)), format)))
                {
                    _targetCombo.Items.Add(format);
                }
            }

            if (_targetCombo.Items.Count > 0)
            {
                _targetCombo.SelectedIndex = 0;
            }
        }
    }

    private static IReadOnlyList<string> FormatsFor(ConversionCategory category) => category switch
    {
        ConversionCategory.Image => ConversionMatrix.ImageFormats,
        ConversionCategory.Video => ConversionMatrix.VideoFormats,
        ConversionCategory.Audio => ConversionMatrix.AudioFormats,
        ConversionCategory.Document => ConversionMatrix.DocumentFormats,
        _ => [],
    };

    private async void OnConvertClick(object? sender, EventArgs e)
    {
        if (_targetCombo.SelectedItem is not string toExt)
        {
            return;
        }

        _convertButton.Enabled = false;
        _results.AppendText($"→ {toExt} に変換...\n");
        foreach (var file in _files)
        {
            var error = await _convert(file, toExt);
            _results.AppendText($"{Path.GetFileName(file)}: {(error ?? "完了")}\n");
        }

        _convertButton.Enabled = true;
    }
}
