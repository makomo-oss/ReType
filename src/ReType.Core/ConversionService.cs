namespace ReType.Core;

/// <summary>Raised by converters when a conversion cannot be performed; the message is shown to the user.</summary>
public sealed class ConversionFailedException : Exception
{
    public ConversionFailedException(string message) : base(message)
    {
    }

    public ConversionFailedException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Replaces the renamed file with the real converted content, or restores the
/// original name when the conversion fails.
/// </summary>
public sealed class ConversionService
{
    private readonly IReadOnlyList<IConverter> _converters;
    private readonly SelfChangeLedger _ledger;
    private readonly IConversionReporter _reporter;
    private readonly TimeSpan _fileReadyTimeout;

    public ConversionService(
        IReadOnlyList<IConverter> converters,
        SelfChangeLedger ledger,
        IConversionReporter reporter,
        TimeSpan? fileReadyTimeout = null)
    {
        _converters = converters;
        _ledger = ledger;
        _reporter = reporter;
        _fileReadyTimeout = fileReadyTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task ProcessRenameAsync(RenameDecision decision, CancellationToken cancellationToken)
    {
        if (decision.Kind == RenameDecisionKind.Ignore)
        {
            return;
        }

        if (decision.Kind == RenameDecisionKind.Unsupported)
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionUnsupported(decision);
            if (!rolledBack)
            {
                _reporter.ConversionFailed(decision, "拡張子をもとに戻せませんでした", false);
            }

            return;
        }

        var target = decision.NewPath;
        var converter = _converters.FirstOrDefault(c => c.CanConvert(decision.FromExtension, decision.ToExtension));
        if (converter is null)
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, "この変換に対応したコンバーターが登録されていません", rolledBack);
            return;
        }

        if (!await WaitForFileReadyAsync(target, cancellationToken))
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, "ファイルが他のプログラムによってロックされています", rolledBack);
            return;
        }

        var directory = Path.GetDirectoryName(target)
            ?? throw new ConversionFailedException("変換先フォルダを特定できません");
        // The temp name keeps the target extension so content-based tools
        // (ffmpeg, pandoc) can infer the output format; the ledger prevents loops.
        var tempPath = Path.Combine(directory, $"retype-{Guid.NewGuid():N}{decision.ToExtension}");

        string? failureReason = null;
        try
        {
            _reporter.ConversionStarted(decision);
            await converter.ConvertAsync(target, decision.FromExtension, decision.ToExtension, tempPath, cancellationToken);

            // Swap the renamed (still old-format) file for the real converted content.
            _ledger.Record(target);
            _ledger.Record(tempPath);
            File.Delete(target);
            File.Move(tempPath, target);
            _reporter.ConversionSucceeded(decision, target);
        }
        catch (OperationCanceledException)
        {
            failureReason = "変換がキャンセルされました";
            throw;
        }
        catch (ConversionFailedException ex)
        {
            failureReason = ex.Message;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
        }
        finally
        {
            TryDelete(tempPath);
        }

        if (failureReason is not null)
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, failureReason, rolledBack);
        }
    }

    /// <summary>Renames the file back to its original name. Returns false when that fails.</summary>
    private bool RestoreOriginalName(RenameDecision decision)
    {
        try
        {
            if (!File.Exists(decision.NewPath) || File.Exists(decision.OldPath))
            {
                return false;
            }

            _ledger.Record(decision.NewPath);
            _ledger.Record(decision.OldPath);
            File.Move(decision.NewPath, decision.OldPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits until the file can be opened exclusively, giving Explorer and
    /// preview handlers time to release their handles after the rename.
    /// </summary>
    private async Task<bool> WaitForFileReadyAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _fileReadyTimeout;
        while (true)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(250, cancellationToken);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort only.
        }
    }
}
