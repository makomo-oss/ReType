using ReType.Core;

namespace ReType.Tests;

public class ConversionQueueTests
{
    [Fact]
    public async Task DisposeAsync_DoesNotDeadlock_WhenQueueWasCreatedOnSynchronizedThread()
    {
        // Regression: the worker used to start on the creating thread and capture
        // its SynchronizationContext (the WinForms UI thread once the message loop
        // is running). TrayContext.RestartWatching / Dispose then blocked that very
        // thread in DisposeAsync, so the worker could never finish and the whole
        // tray app froze ("Exit" stopped responding).
        var queue = CreateQueueOnNeverPumpingThread();

        var dispose = Task.Run(() => queue.DisposeAsync().AsTask().GetAwaiter().GetResult());
        var finished = await Task.WhenAny(dispose, Task.Delay(5000));

        Assert.True(finished == dispose, "DisposeAsync deadlocked: the worker needs the creating thread to complete.");
#pragma warning disable xUnit1031 // intentional: the task has already completed, this only surfaces its exception
        dispose.GetAwaiter().GetResult();
#pragma warning restore xUnit1031
    }

    private static ConversionQueue CreateQueueOnNeverPumpingThread()
    {
        ConversionQueue? queue = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NeverPumpingContext());
            queue = new ConversionQueue((_, _) => Task.CompletedTask);
        });
        thread.Start();
        thread.Join();
        return queue!;
    }

    /// <summary>Stands in for a blocked UI thread: posted continuations never run.</summary>
    private sealed class NeverPumpingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
        }
    }
}
