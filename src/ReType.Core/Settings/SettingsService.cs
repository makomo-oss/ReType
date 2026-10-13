using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReType.Core.Settings;

/// <summary>Loads and saves settings.json under %APPDATA%\ReType.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReType");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? AppSettings.CreateDefault();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Fall back to defaults for a corrupt or unreadable settings file.
        }

        return AppSettings.CreateDefault();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
