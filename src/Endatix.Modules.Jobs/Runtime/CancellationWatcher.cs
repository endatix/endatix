using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Re-reads a running job's row on an interval and trips the handler's token when the row says <c>Canceled</c>,
/// or when the row no longer belongs to this attempt.
/// </summary>
/// <remarks>
/// <para>
/// A cancellation is written to the row by whichever node served the request, so a watcher on the node running
/// the job is what carries it to the handler. It only reads.
/// </para>
/// <para>
/// A node that stopped checking in for a while is presumed dead and its job recovered elsewhere, although it may
/// still be running it. The recovery takes a new attempt, so this attempt's fenced writes can no longer land;
/// stopping its handler as soon as the row shows another attempt keeps the two from doing the work side by side.
/// </para>
/// </remarks>
internal sealed class CancellationWatcher : IAsyncDisposable
{
    private readonly CancellationTokenSource _tripped = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _watching;
    private volatile bool _sawCancellation;
    private volatile bool _sawSupersession;

    private CancellationWatcher(
        IServiceScopeFactory scopeFactory,
        long jobId,
        int claimedAttempt,
        TimeSpan interval,
        ILogger logger)
    {
        _watching = WatchAsync(scopeFactory, jobId, claimedAttempt, interval, logger);
    }

    /// <summary>Cancelled once the row is seen <c>Canceled</c>, or taken over by another attempt.</summary>
    public CancellationToken Token => _tripped.Token;

    public bool SawCancellation => _sawCancellation;

    /// <summary>Whether the row was seen to belong to another attempt, or to be gone.</summary>
    public bool SawSupersession => _sawSupersession;

    public static CancellationWatcher Start(
        IServiceScopeFactory scopeFactory,
        long jobId,
        int claimedAttempt,
        TimeSpan interval,
        ILogger logger) =>
        new(scopeFactory, jobId, claimedAttempt, interval, logger);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _watching;
        _stop.Dispose();
        _tripped.Dispose();
    }

    private async Task WatchAsync(
        IServiceScopeFactory scopeFactory,
        long jobId,
        int claimedAttempt,
        TimeSpan interval,
        ILogger logger)
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
                    var state = await repository.ReadAttemptAsync(jobId, _stop.Token);

                    if (state is { Status: JobStatus.Canceled })
                    {
                        _sawCancellation = true;
                        await _tripped.CancelAsync();
                        return;
                    }

                    if (state is not { Status: JobStatus.Processing } || state.AttemptCount != claimedAttempt)
                    {
                        _sawSupersession = true;
                        await _tripped.CancelAsync();
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
