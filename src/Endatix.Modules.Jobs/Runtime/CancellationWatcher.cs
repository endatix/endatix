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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatchedAttempt _watched;
    private readonly ILogger _logger;
    private readonly Task _watching;
    private volatile bool _sawCancellation;
    private volatile bool _sawSupersession;

    private CancellationWatcher(IServiceScopeFactory scopeFactory, WatchedAttempt watched, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _watched = watched;
        _logger = logger;
        _watching = WatchAsync();
    }

    /// <summary>Cancelled once the row is seen <c>Canceled</c>, or taken over by another attempt.</summary>
    public CancellationToken Token => _tripped.Token;

    public bool SawCancellation => _sawCancellation;

    /// <summary>Whether the row was seen to belong to another attempt, or to be gone.</summary>
    public bool SawSupersession => _sawSupersession;

    public static CancellationWatcher Start(IServiceScopeFactory scopeFactory, WatchedAttempt watched, ILogger logger) =>
        new(scopeFactory, watched, logger);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _watching;
        _stop.Dispose();
        _tripped.Dispose();
    }

    private async Task WatchAsync()
    {
        try
        {
            await PollUntilTrippedAsync();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // The attempt ended.
        }
    }

    private async Task PollUntilTrippedAsync()
    {
        using var timer = new PeriodicTimer(_watched.Interval);
        var tripped = false;
        while (!tripped && await timer.WaitForNextTickAsync(_stop.Token))
        {
            tripped = await PollAsync();
        }
    }

    // Returns whether the row was seen to have moved on, which trips the handler's token.
    private async Task<bool> PollAsync()
    {
        try
        {
            var state = await _scopeFactory.WithStateRepositoryAsync(
                repository => repository.ReadAttemptAsync(_watched.Attempt.JobId, _stop.Token));
            return await TripIfMovedOnAsync(state);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One failed read must not stop the job; the next tick tries again.
            _logger.LogWarning(exception, "Checking background job {JobId} for cancellation failed", _watched.Attempt.JobId);
            return false;
        }
    }

    private async Task<bool> TripIfMovedOnAsync(JobAttemptState? state)
    {
        if (state is { Status: JobStatus.Canceled })
        {
            _sawCancellation = true;
        }
        else if (state is not { Status: JobStatus.Processing } || state.AttemptCount != _watched.Attempt.AttemptCount)
        {
            _sawSupersession = true;
        }
        else
        {
            return false;
        }

        await _tripped.CancelAsync();
        return true;
    }
}

/// <param name="Attempt">The attempt whose row is watched.</param>
/// <param name="Interval">How often the row is read.</param>
internal readonly record struct WatchedAttempt(AttemptRef Attempt, TimeSpan Interval);
