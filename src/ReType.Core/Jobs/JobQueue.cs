using System.Collections.Concurrent;
using ReType.Core.Config;
using ReType.Core.Converters;

namespace ReType.Core.Jobs;

/// <summary>ジョブ処理完了時の通知（GUI の進捗表示用）。</summary>
public sealed record JobOutcome(ConversionJob Job, bool Success, string? OutputPath, string? Error);

/// <summary>
/// 単一スレッドで順次処理するジョブキュー。
/// KeepOriginal が有効なら、変換前に元内容を旧名で保存してから上書きする。
/// </summary>
public sealed class JobQueue : IDisposable
{
    private readonly BlockingCollection<ConversionJob> queue = new();
    private readonly ConverterRegistry registry;
    private readonly Settings settings;
    private readonly SelfWriteTracker tracker;
    private readonly CancellationTokenSource cts = new();
    private readonly Task worker;

    /// <summary>各ジョブの完了時に 1 回呼ばれる（成功・失敗どちらも）。</summary>
    public event Action<JobOutcome>? Completed;

    public JobQueue(ConverterRegistry registry, Settings settings, SelfWriteTracker tracker)
    {
        this.registry = registry;
        this.settings = settings;
        this.tracker = tracker;
        worker = Task.Run(RunAsync);
    }

    public void Enqueue(ConversionJob job) => queue.Add(job);

    private async Task RunAsync()
    {
        foreach (var job in queue.GetConsumingEnumerable(cts.Token))
        {
            JobOutcome outcome;
            try
            {
                var converter = registry.Resolve(job.SourceFamily, job.TargetFamily);
                if (converter is null)
                {
                    outcome = new JobOutcome(job, false, null,
                        $"{job.SourceFamily}→{job.TargetFamily} はサポートされていない組み合わせです");
                }
                else
                {
                    string input = job.InputPath;
                    if (settings.KeepOriginal)
                    {
                        // 元内容を旧名で保存（リネームで元の名前を復元）。
                        tracker.Add(job.OriginalPath);
                        File.Move(input, job.OriginalPath, overwrite: true);
                        input = job.OriginalPath;
                    }

                    string output = job.InputPath;
                    tracker.Add(output);
                    var result = await converter.ConvertAsync(input, output, settings, cts.Token);
                    outcome = new JobOutcome(job, result.Success, result.Success ? output : null, result.Error);
                }
            }
            catch (Exception ex)
            {
                outcome = new JobOutcome(job, false, null, ex.Message);
            }

            Completed?.Invoke(outcome);
        }
    }

    public void Dispose()
    {
        queue.CompleteAdding();
        cts.Cancel();
        try { worker.Wait(); } catch { }
        cts.Dispose();
        queue.Dispose();
    }
}
