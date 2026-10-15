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

        var okButton = form.Controls
            .OfType<Button>()
            .First(b => b.Text == "OK");
        okButton.PerformClick();

        Assert.Single(form.ResultSettings.WatchedFolders);
        Assert.Contains("C:\\Temp", form.ResultSettings.WatchedFolders);
        Assert.True(form.ResultSettings.EnableImageConversions);
        Assert.False(form.ResultAutoStart);
    }
}
