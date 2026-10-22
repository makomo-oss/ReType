using System.Text.Json;
using System.Text.Json.Serialization;
using ReType.Core.Settings;

namespace ReType.Core;

public enum ConversionOutcome
{
    Succeeded,
    Failed,
    Unsupported,
    Undone,
}

/// <summary>One logged conversion. OldPath/NewPath follow the rename decision.</summary>
public sealed record ConversionEntry(
    DateTimeOffset Timestamp,
    string OldPath,
    string NewPath,
    string FromExtension,
    string ToExtension,
    ConversionOutcome Outcome,
    string? Reason = null);

/// <summary>
/// Persistent log of recent conversions (newest first, capped) stored as JSON
/// next to settings.json. Shared by the settings history view and Undo.
/// </summary>
public sealed class ConversionHistoryStore
{
    public const int MaxEntries = 100;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly string _path;

    public ConversionHistoryStore(string? path = null) =>
        _path = path ?? Path.Combine(SettingsService.SettingsDirectory, "history.json");

    public List<ConversionEntry> Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    return JsonSerializer.Deserialize<List<ConversionEntry>>(json, SerializerOptions) ?? [];
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // A corrupt history starts over; it is not worth failing conversions over.
            }

            return [];
        }
    }

    public void Add(ConversionEntry entry)
    {
        lock (_gate)
        {
            var entries = Load();
            entries.Insert(0, entry);
            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            }

            Save(entries);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private void Save(List<ConversionEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(entries, SerializerOptions));
    }
}
