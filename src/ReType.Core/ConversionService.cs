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
    private readonly ConversionHistoryStore? _history;

    public ConversionService(
        IReadOnlyList<IConverter> converters,
        SelfChangeLedger ledger,
        IConversionReporter reporter,
        TimeSpan? fileReadyTimeout = null,
        ConversionHistoryStore? history = null)
    {
        _converters = converters;
        _ledger = ledger;
        _reporter = reporter;
        _fileReadyTimeout = fileReadyTimeout ?? TimeSpan.FromSeconds(5);
        _history = history;
    }

    /// <summary>
    /// Replaces the renamed file with converted content, or restores the
    /// original name on failure. Returns true when no conversion is needed or
    /// the conversion succeeded; false when it failed or was unsupported.
    /// </summary>
    public async Task<bool> ProcessRenameAsync(RenameDecision decision, CancellationToken cancellationToken)
    {
        if (decision.Kind == RenameDecisionKind.Ignore)
        {
            return true;
        }

        if (decision.Kind == RenameDecisionKind.Unsupported)
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionUnsupported(decision);
            Record(decision, ConversionOutcome.Unsupported);
            if (!rolledBack)
            {
                _reporter.ConversionFailed(decision, "拡張子をもとに戻せませんでした", false);
            }

            return false;
        }

        var target = decision.NewPath;
        var converter = _converters.FirstOrDefault(c => c.CanConvert(decision.FromExtension, decision.ToExtension));
        if (converter is null)
        {
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, "この変換に対応したコンバーターが登録されていません", rolledBack);
            Record(decision, ConversionOutcome.Failed, "コンバーターが登録されていません");
            return false;
        }

        var readiness = await WaitForFileReadyAsync(target, cancellationToken);
        if (readiness != FileReadiness.Ready)
        {
            var reason = readiness == FileReadiness.Missing
                ? "ファイルが見つかりません"
                : "ファイルが他のプログラムによってロックされています";
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, reason, rolledBack);
            Record(decision, ConversionOutcome.Failed, reason);
            return false;
        }

        var directory = Path.GetDirectoryName(target);
        if (directory is null)
        {
            // Was a throw before: the queue swallowed it and no balloon appeared.
            var rolledBack = RestoreOriginalName(decision);
            _reporter.ConversionFailed(decision, "変換先フォルダを特定できません", rolledBack);
            Record(decision, ConversionOutcome.Failed, "変換先フォルダを特定できません");
            return false;
        }
        // The temp name keeps the target extension so content-based tools
        // (ffmpeg, pandoc) can infer the output format; the ledger prevents loops.
        var tempPath = Path.Combine(directory, $"retype-{Guid.NewGuid():N}{decision.ToExtension}");

        string? failureReason = null;
        try
        {
            _reporter.ConversionStarted(decision);
            var progress = new DirectProgress<ConversionProgress>(p => _reporter.ConversionProgress(decision, p.Percent));
            await converter.ConvertAsync(target, decision.FromExtension, decision.ToExtension, tempPath, progress, cancellationToken);

            // Swap the renamed (still old-format) file for the real converted content.
            _ledger.Record(target);
            _ledger.Record(tempPath);
            File.Delete(target);
            File.Move(tempPath, target);
            _reporter.ConversionSucceeded(decision, target);
            Record(decision, ConversionOutcome.Succeeded);
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
            Record(decision, ConversionOutcome.Failed, failureReason);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts a file in place for drag-and-drop: renames it to the target
    /// extension (ledger-recorded so the watcher ignores it) and runs the same
    /// conversion pipeline, which rolls the name back on failure. Returns null
    /// on success, otherwise a user-facing reason.
    /// </summary>
    public async Task<string?> ConvertInPlaceAsync(string path, string toExt, CancellationToken cancellationToken)
    {
        var fromExt = ConversionMatrix.Normalize(Path.GetExtension(path));
        toExt = ConversionMatrix.Normalize(toExt);

        if (!ConversionMatrix.IsSupported(fromExt, toExt))
        {
            return $"{fromExt} → {toExt} は未対応です";
        }

        if (_converters.All(c => !c.CanConvert(fromExt, toExt)))
        {
            return "この変換に対応したコンバーターが登録されていません（カテゴリが OFF かも）";
        }

        var newPath = Path.ChangeExtension(path, null) + toExt;
        if (File.Exists(newPath))
        {
            return $"変換先のファイルが既に存在します: {Path.GetFileName(newPath)}";
        }

        try
        {
            // Ledger first: the watcher must not treat our rename as a user request.
            _ledger.Record(path);
            _ledger.Record(newPath);
            File.Move(path, newPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }

        var decision = new RenameDecision(RenameDecisionKind.Convert, path, newPath, fromExt, toExt);
        var succeeded = await ProcessRenameAsync(decision, cancellationToken);
        return succeeded ? null : "変換に失敗しました（元の名前に戻しました）";
    }

    /// <summary>
    /// Undoes a successful conversion: converts the current content back to
    /// the original format and restores the original name. Returns null on
    /// success, otherwise a user-facing reason. Lossy formats re-encode, so
    /// the restored content is equivalent but not bit-identical.
    /// </summary>
    public async Task<string?> UndoAsync(ConversionEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Outcome != ConversionOutcome.Succeeded)
        {
            return "この項目は元に戻せません";
        }

        if (!File.Exists(entry.NewPath))
        {
            return $"ファイルが見つかりません: {Path.GetFileName(entry.NewPath)}";
        }

        if (File.Exists(entry.OldPath))
        {
            return $"元の名前のファイルが既に存在します: {Path.GetFileName(entry.OldPath)}";
        }

        var converter = _converters.FirstOrDefault(c => c.CanConvert(entry.ToExtension, entry.FromExtension));
        if (converter is null)
        {
            return $"{entry.ToExtension} → {entry.FromExtension} の逆変換に対応していません";
        }

        var directory = Path.GetDirectoryName(entry.NewPath);
        if (directory is null)
        {
            return "変換先フォルダを特定できません";
        }

        var tempPath = Path.Combine(directory, $"retype-{Guid.NewGuid():N}{entry.FromExtension}");
        try
        {
            await converter.ConvertAsync(entry.NewPath, entry.ToExtension, entry.FromExtension, tempPath, cancellationToken);

            _ledger.Record(entry.NewPath);
            _ledger.Record(tempPath);
            _ledger.Record(entry.OldPath);
            File.Delete(entry.NewPath);
            File.Move(tempPath, entry.OldPath);
        }
        catch (Exception ex) when (ex is ConversionFailedException or IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
        finally
        {
            TryDelete(tempPath);
        }

        _history?.Add(entry with { Timestamp = DateTimeOffset.Now, Outcome = ConversionOutcome.Undone, Reason = null });
        return null;
    }

    private void Record(RenameDecision decision, ConversionOutcome outcome, string? reason = null) =>
        _history?.Add(new ConversionEntry(
            DateTimeOffset.Now,
            decision.OldPath,
            decision.NewPath,
            decision.FromExtension,
            decision.ToExtension,
            outcome,
            reason));

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

    private enum FileReadiness
    {
        Ready,
        Missing,
        Locked,
    }

    /// <summary>
    /// Waits until the file can be opened exclusively, giving Explorer and
    /// preview handlers time to release their handles after the rename.
    /// </summary>
    private async Task<FileReadiness> WaitForFileReadyAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _fileReadyTimeout;
        while (true)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return FileReadiness.Ready;
            }
            catch (FileNotFoundException)
            {
                return FileReadiness.Missing;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(250, cancellationToken);
            }
            catch (IOException)
            {
                return FileReadiness.Locked;
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

    /// <summary>IProgress that invokes the handler synchronously on the reporting thread.</summary>
    private sealed class DirectProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
