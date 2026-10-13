using System.Diagnostics;
using System.Text;

namespace ReType.Converters;

/// <summary>Locates and runs external tool executables (ffmpeg, pandoc).</summary>
public static class ToolRunner
{
    public sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// Resolves a tool executable by name. Search order: the thirdparty\bin folder
    /// next to the running app, then the PATH environment variable.
    /// </summary>
    public static string? Locate(string executableName)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "thirdparty", "bin", executableName);
        if (File.Exists(local))
        {
            return local;
        }

        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (entry.Length == 0)
            {
                continue;
            }

            try
            {
                var candidate = Path.Combine(entry.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Malformed PATH entries are skipped.
            }
        }

        return null;
    }

    public static async Task<ToolResult> RunAsync(
        string executable,
        string arguments,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {executable}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is not null)
        {
            timeoutCts.CancelAfter(timeout.Value);
        }

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException($"{Path.GetFileName(executable)} timed out");
        }

        return new ToolResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
