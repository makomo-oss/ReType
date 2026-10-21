using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ReType.Core;

/// <summary>
/// Serializes conversion jobs so that at most one conversion runs at a time
/// and duplicate events for the same target path are collapsed.
/// </summary>
public sealed class ConversionQueue : IAsyncDisposable
{
    private readonly Channel<RenameDecision> _channel = Channel.CreateUnbounded<RenameDecision>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<RenameDecision, CancellationToken, Task> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public ConversionQueue(Func<RenameDecision, CancellationToken, Task> handler)
    {
        _handler = handler;
        // Task.Run keeps the worker and its await continuations on the thread pool
        // even when the queue is created on the WinForms UI thread (which owns a
        // SynchronizationContext). Without this, disposing the queue while blocking
        // that thread (TrayContext.RestartWatching / Dispose) deadlocks the UI.
        _worker = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Enqueue(RenameDecision decision)
    {
        var key = Path.GetFullPath(decision.NewPath);
        if (!_pending.TryAdd(key, 0))
        {
            return;
        }

        _channel.Writer.TryWrite(decision);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            // ConfigureAwait(false) everywhere: the queue may be created on the
            // WinForms UI thread, and its continuations must never require that
            // thread to pump messages (it blocks while restarting/disposing us).
            await foreach (var decision in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _handler(decision, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // A crashing job must not take down the queue.
                }
                finally
                {
                    _pending.TryRemove(Path.GetFullPath(decision.NewPath), out _);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            // ConfigureAwait(false) is load-bearing: callers dispose us while
            // blocking their thread (TrayContext.RestartWatching / Dispose run
            // on the UI thread); resuming this continuation on that thread would
            // deadlock it.
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
    }
}
