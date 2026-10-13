namespace ReType.Core.Settings;

public sealed record AppSettings
{
    /// <summary>Folders whose renames trigger conversions.</summary>
    public List<string> WatchedFolders { get; init; } = [];

    /// <summary>Whether watched folders are watched recursively.</summary>
    public bool IncludeSubdirectories { get; init; }

    /// <summary>Convert image renames (jpg/png/webp/...).</summary>
    public bool EnableImageConversions { get; init; } = true;

    /// <summary>Convert video container renames (mp4/mkv/...).</summary>
    public bool EnableVideoConversions { get; init; } = true;

    /// <summary>Convert audio renames (mp3/flac/...).</summary>
    public bool EnableAudioConversions { get; init; } = true;

    /// <summary>Convert document renames (md/docx/...).</summary>
    public bool EnableDocumentConversions { get; init; } = true;

    /// <summary>Default folders: Desktop, Downloads, Documents, Pictures, Videos.</summary>
    public static AppSettings CreateDefault()
    {
        var folders = new List<string>();
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory,
                     Environment.SpecialFolder.UserProfile,
                     Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.MyPictures,
                     Environment.SpecialFolder.MyVideos,
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (path.Length > 0 && Directory.Exists(path))
            {
                folders.Add(path);
            }
        }

        // Downloads has no SpecialFolder value; resolve it relative to the user profile.
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            folders.Add(downloads);
        }

        return new AppSettings { WatchedFolders = folders };
    }
}
