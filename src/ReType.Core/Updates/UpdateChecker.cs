using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace ReType.Core.Updates;

/// <summary>
/// Checks GitHub Releases for a newer ReType version and downloads the installer.
/// </summary>
public sealed class UpdateChecker
{
    public const string DefaultRepository = "makomo-oss/ReType";

    private static readonly HttpClient Http = CreateHttpClient();

    public string Repository { get; }

    public UpdateChecker(string repository = DefaultRepository)
    {
        Repository = repository;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ReType-updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>The latest published release, or null when the repository has none yet.</summary>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases/latest", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ReleaseInfo.Parse(json);
    }

    public static bool IsUpdateAvailable(ReleaseInfo? release, Version current) =>
        release?.Version is not null && release.Version > current;

    /// <summary>Downloads the release installer to <paramref name="destinationPath"/>.</summary>
    public async Task DownloadInstallerAsync(
        ReleaseInfo release,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var partialPath = destinationPath + ".partial";
        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(partialPath))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Move(partialPath, destinationPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                }
            }
            catch (IOException)
            {
            }

            throw;
        }
    }
}

/// <summary>
/// Hands the downloaded installer to a detached helper process that runs the
/// silent install, restarts the app, and cleans up the installer file.
/// The caller is expected to exit right after this call.
/// </summary>
public static class UpdateApplier
{
    /// <summary>
    /// Hands the downloaded installer to a detached helper process that stops
    /// the running app, runs the silent install (with one retry), restarts the
    /// app, and cleans up the installer file. The caller should exit right after.
    /// </summary>
    /// <param name="appExePath">App to relaunch after install; null to skip the relaunch.</param>
    public static void ApplyAndRestart(string installerPath, string? appExePath)
    {
        installerPath = Path.GetFullPath(installerPath);

        string InstallCmd(string path) =>
            "$p = Start-Process -FilePath '" + path + "' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru";

        var script =
            // The Restart Manager does not reliably close tray apps; stop it explicitly.
            "Get-Process -Name 'ReType' -ErrorAction SilentlyContinue | Stop-Process -Force; " +
            "Start-Sleep -Seconds 2; " +
            InstallCmd(installerPath) + "; " +
            "if ($p.ExitCode -ne 0) { Start-Sleep -Seconds 3; " + InstallCmd(installerPath) + " }";

        if (appExePath is not null)
        {
            script += "; if ($p.ExitCode -eq 0) { Start-Process -FilePath '" + Path.GetFullPath(appExePath) + "' }";
        }

        script += "; Remove-Item -LiteralPath '" + installerPath + "' -Force";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
