using System.Windows.Forms;
using ReType.App;
using ReType.Core.Settings;
using ReType.Core.Updates;
using Xunit;

namespace ReType.Tests;

public class SettingsFormTests
{
    [Fact]
    public void Constructor_BuildsAllSections()
    {
        var settings = new AppSettings { WatchedFolders = ["C:\\Temp"] };
        using var form = new SettingsForm(
            settings,
            autoStartEnabled: true,
            new Version(1, 2, 3),
            () => Task.FromResult<ReleaseInfo?>(null),
            _ => Task.CompletedTask);

        Assert.Equal("ReType 設定", form.Text);

        var texts = form.Controls.Cast<Control>()
            .SelectMany(c => c.Controls.Cast<Control>())
            .OfType<Label>()
            .Select(l => l.Text)
            .ToList();
        Assert.Contains("現在のバージョン: 1.2.3", texts);

        var checkBoxes = form.Controls.Cast<Control>()
            .SelectMany(c => c.Controls.Cast<Control>())
            .OfType<CheckBox>()
            .ToList();
        Assert.Contains(checkBoxes, c => c.Text == "サブフォルダも監視する");
        Assert.Contains(checkBoxes, c => c.Text == "画像");
        Assert.Contains(checkBoxes, c => c.Text == "Windows の起動時に ReType を自動的に開始する");
    }

    [Fact]
    public void OkClick_CarriesEditedValues()
    {
        var settings = new AppSettings { WatchedFolders = ["C:\\Temp"], IncludeSubdirectories = false };
        using var form = new SettingsForm(
            settings,
            autoStartEnabled: false,
            new Version(1, 0, 0),
            () => Task.FromResult<ReleaseInfo?>(null),
            _ => Task.CompletedTask);
        form.Show();

        var okButton = form.Controls
            .OfType<Button>()
            .First(b => b.Text == "OK");
        okButton.PerformClick();

        Assert.Single(form.ResultSettings.WatchedFolders);
        Assert.Contains("C:\\Temp", form.ResultSettings.WatchedFolders);
        Assert.True(form.ResultSettings.EnableImageConversions);
        Assert.False(form.ResultAutoStart);
    }

    [Fact]
    public void OkClick_ClosesTheModelessForm()
    {
        // Regression: the form is shown modelessly, where Button.DialogResult alone
        // does nothing; OK/Cancel must close it explicitly or it can never be
        // dismissed with the buttons.
        var form = CreateForm();
        form.Show();
        var okButton = form.Controls.OfType<Button>().First(b => b.Text == "OK");

        okButton.PerformClick();

        Assert.Equal(DialogResult.OK, form.DialogResult);
        Assert.True(form.IsDisposed); // the form closes (and disposes) on OK
    }

    [Fact]
    public void CancelClick_ClosesTheFormWithoutCommittingEdits()
    {
        var settings = new AppSettings { WatchedFolders = ["C:\\Temp"] };
        using var form = new SettingsForm(
            settings,
            autoStartEnabled: false,
            new Version(1, 0, 0),
            () => Task.FromResult<ReleaseInfo?>(null),
            _ => Task.CompletedTask);
        form.Show();
        var cancelButton = form.Controls.OfType<Button>().First(b => b.Text == "キャンセル");

        cancelButton.PerformClick();

        Assert.Equal(DialogResult.Cancel, form.DialogResult);
        Assert.True(form.IsDisposed);
        Assert.Same(settings, form.ResultSettings);
    }

    [Fact]
    public void CategorySection_ListsSupportedFormats()
    {
        // The UI must show what each category accepts, straight from the
        // ConversionMatrix so the two can never drift apart.
        using var form = CreateForm();

        var texts = form.Controls.Cast<Control>()
            .SelectMany(c => c.Controls.Cast<Control>())
            .OfType<Label>()
            .Select(l => l.Text)
            .ToList();

        Assert.Contains(texts, t => t.StartsWith("画像:") && t.Contains("webp") && t.Contains("png"));
        Assert.Contains(texts, t => t.StartsWith("動画:") && t.Contains("mp4") && t.Contains("mkv"));
        Assert.Contains(texts, t => t.StartsWith("音声:") && t.Contains("flac"));
        Assert.Contains(texts, t => t.StartsWith("文書:") && t.Contains("docx"));
        Assert.Contains(texts, t => t.Contains("カテゴリをまたぐ変換"));
    }

    private static SettingsForm CreateForm() => new(
        new AppSettings(),
        autoStartEnabled: false,
        new Version(1, 0, 0),
        () => Task.FromResult<ReleaseInfo?>(null),
        _ => Task.CompletedTask);
}
