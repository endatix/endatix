using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Re-reads a running job's status on an interval and trips the handler's token when the row says
/// <c>Canceled</c>.
/// </summary>
/// <remarks>
/// A cancellation is written to the row by whichever node served the request, so a watcher on the node running
/// the job is what carries it to the handler. It only reads.
/// </remarks>
internal sealed class CancellationWatcher : IAsyncDisposable
{
    private readonly CancellationTokenSource _canceled = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _watching;

    private CancellationWatcher(
        IServiceScopeFactory scopeFactory,
        long jobId,
        TimeSpan interval,
        ILogger logger)
    {
        _watching = WatchAsync(scopeFactory, jobId, interval, logger);
    }

    /// <summary>Cancelled once the row is seen <c>Canceled</c>.</summary>
    public CancellationToken Token => _canceled.Token;

    public bool SawCancellation => _canceled.IsCancellationRequested;

    public static CancellationWatcher Start(
        IServiceScopeFactory scopeFactory,
        long jobId,
        TimeSpan interval,
        ILogger logger) =>
        new(scopeFactory, jobId, interval, logger);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _watching;
        _stop.Dispose();
        _canceled.Dispose();
    }

    private async Task WatchAsync(IServiceScopeFactory scopeFactory, long jobId, TimeSpan interval, ILogger logger)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>();
                    if (await repository.ReadStatusAsync(jobId, _stop.Token) is JobStatus.Canceled)
                    {
                        await _canceled.CancelAsync();
                        return;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // One failed read must not stop the job; the next tick tries again.
                    logger.LogWarning(exception, "Checking background job {JobId} for cancellation failed", jobId);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The attempt ended.
        }
    }
}
