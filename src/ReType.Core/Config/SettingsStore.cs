using System.Text.Json;

namespace ReType.Core.Config;

/// <summary>%APPDATA%\ReType\settings.json の読み書き。</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string path;

    public SettingsStore(string? path = null)
    {
        this.path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReType", "settings.json");
    }

    public string Path => this.path;

    public Settings Load()
    {
        if (!File.Exists(path))
            return new Settings();

        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
        }
        catch (JsonException)
        {
            // 壊れた設定は黙ってデフォルトで再開始（次回保存で修復される）
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
