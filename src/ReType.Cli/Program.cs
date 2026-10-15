using ReType.Converters;
using ReType.Core;
using ReType.Core.Settings;
using ReType.Core.Updates;

namespace ReType.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "convert" => await RunConvertAsync(args),
            "watch" => await RunWatchAsync(args),
            "update" => await RunUpdateAsync(args),
            _ => PrintUsage(),
        };
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            ReType CLI

            Usage:
              retype convert <inputFile> <targetExtension>   Convert a file to a new file (original untouched)
              retype watch [folder ...]                      Watch folders for extension-change renames
              retype update check                            Show the latest release vs the current version
              retype update apply                            Download and silently install the latest release
            """);
        return 1;
    }

    private static async Task<int> RunConvertAsync(string[] args)
    {
        if (args.Length < 3)
        {
            PrintUsage();
            return 1;
        }

        var inputPath = Path.GetFullPath(args[1]);
        var toExt = args[2].ToLowerInvariant();
        if (!toExt.StartsWith('.'))
        {
            toExt = "." + toExt;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Input file not found: {inputPath}");
            return 1;
        }

        var fromExt = ConversionMatrix.Normalize(Path.GetExtension(inputPath));
        if (!ConversionMatrix.IsSupported(fromExt, toExt))
        {
            Console.Error.WriteLine($"Conversion {fromExt} -> {toExt} is not supported.");
            return 1;
        }

        var converter = CreateConverters().FirstOrDefault(c => c.CanConvert(fromExt, toExt));
        if (converter is null)
        {
            Console.Error.WriteLine($"No converter registered for {fromExt} -> {toExt}.");
            return 1;
        }

        var outputPath = Path.ChangeExtension(inputPath, null) + toExt;
        try
        {
            await converter.ConvertAsync(inputPath, fromExt, toExt, outputPath, CancellationToken.None);
            Console.WriteLine($"Converted: {inputPath} -> {outputPath}");
            return 0;
        }
        catch (Exception ex) when (ex is ConversionFailedException or TimeoutException)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunWatchAsync(string[] args)
    {
        var settings = new SettingsService().Load();
        var folders = args.Length > 1 ? args[1..] : settings.WatchedFolders.ToArray();
        if (folders.Length == 0)
        {
            Console.Error.WriteLine("No folders to watch. Pass folders as arguments or configure settings.json.");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var ledger = new SelfChangeLedger();
        var service = new ConversionService(CreateConverters(), ledger, new ConsoleReporter());
        await using var queue = new ConversionQueue(service.ProcessRenameAsync);
        var detector = new RenameDetector();

        using var watcher = new FileWatcherService(ledger);
        watcher.ExternalRenameDetected += (oldPath, newPath) => queue.Enqueue(detector.Detect(oldPath, newPath));
        watcher.Watch(folders, settings.IncludeSubdirectories);

        Console.WriteLine("Watching (Ctrl+C to stop):");
        foreach (var folder in folders)
        {
            Console.WriteLine($"  {folder}");
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }

    private static async Task<int> RunUpdateAsync(string[] args)
    {
        var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "check";
        var checker = new UpdateChecker();
        ReleaseInfo? release;
        try
        {
            release = await checker.GetLatestReleaseAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update check failed: {ex.Message}");
            return 2;
        }

        var current = typeof(Program).Assembly.GetName().Version ?? new Version(0, 0);
        if (release is null)
        {
            Console.WriteLine("No published release found.");
            return 1;
        }

        Console.WriteLine($"Latest:  {release.TagName} ({release.AssetName})");
        Console.WriteLine($"Current: {current}");
        if (!UpdateChecker.IsUpdateAvailable(release, current))
        {
            Console.WriteLine("Up to date.");
            return 0;
        }

        if (sub != "apply")
        {
            Console.WriteLine($"Update available: {release.Version} (run 'update apply' to install)");
            return 0;
        }

        var installerPath = Path.Combine(Path.GetTempPath(), release.AssetName);
        Console.WriteLine($"Downloading {release.DownloadUrl}");
        await checker.DownloadInstallerAsync(release, installerPath);
        Console.WriteLine($"Downloaded to {installerPath}; running silent install...");

        var installedApp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ReType", "ReType.exe");
        UpdateApplier.ApplyAndRestart(installerPath, File.Exists(installedApp) ? installedApp : null);
        return 0;
    }

    private static IReadOnlyList<IConverter> CreateConverters() =>
    [
        new ImageConverter(),
        new WebPConverter(),
        new FfmpegConverter(),
        new PandocConverter(),
    ];

    private sealed class ConsoleReporter : IConversionReporter
    {
        public void ConversionStarted(RenameDecision decision) =>
            Console.WriteLine($"[convert] {decision.FromExtension} -> {decision.ToExtension}: {decision.NewPath}");

        public void ConversionSucceeded(RenameDecision decision, string outputPath) =>
            Console.WriteLine($"[done]    {outputPath}");

        public void ConversionFailed(RenameDecision decision, string reason, bool rolledBack) =>
            Console.Error.WriteLine($"[failed]  {decision.NewPath}: {reason}{(rolledBack ? " (name restored)" : string.Empty)}");

        public void ConversionUnsupported(RenameDecision decision) =>
            Console.Error.WriteLine(
                $"[skip]    {decision.FromExtension} -> {decision.ToExtension} is not supported; name restored: {decision.OldPath}");
    }
}
