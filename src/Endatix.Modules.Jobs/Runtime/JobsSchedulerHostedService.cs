using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Starts and stops the jobs scheduler with the host.
/// </summary>
/// <remarks>
/// <para>
/// Registered after the migration service, so the scheduler validates its tables only once they exist. Building
/// the scheduler is what validates them, which is why a schedule-only host builds it too: a node whose tables are
/// missing or outdated fails at startup rather than at its first enqueue.
/// </para>
/// <para>
/// A database that cannot be reached at all is a different failure. The host stays up — its readiness probe
/// reports the outage — and the scheduler starts once the database answers, because restarting the process
/// cannot fix a database. That holds for an outage at any step of the start, not only the first connection. A
/// database that answers and refuses, such as a login it does not accept, fails the start instead: waiting would
/// never fix it.
/// </para>
/// <para>
/// A SQL Server node set to enqueue without the trigger lock is refused before its scheduler is built unless the
/// database reads committed snapshots (<see cref="ReadCommittedSnapshotGatedSchedulerFactory"/>): at boot the host fails to
/// start, and after a deferred start the refusal is logged as Critical and every enqueue on the node fails.
/// </para>
/// </remarks>
internal sealed class JobsSchedulerHostedService(
    IServiceProvider services,
    JobStoreDatabase database,
    JobHandlerRegistry registry,
    IOptions<BackgroundJobsOptions> options,
    JobsShutdownSignal shutdownSignal,
    ILogger<JobsSchedulerHostedService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan UnreachableRetryDelay = TimeSpan.FromSeconds(5);

    // A lost race clears as soon as the other transaction ends, so the first few are tried again sooner than an
    // outage; one that keeps coming back is something else, and is tried at the outage's pace.
    private static readonly TimeSpan ContendedRetryDelay = TimeSpan.FromSeconds(1);
    private const int QuickContendedTries = 3;

    // A lost race, or an outage, is logged the first time and then one in this many tries, so one that keeps recurring
    // is seen without flooding the log.
    private const int TriesPerRepeatedWarning = 12;

    // A start still waiting for the database stops waiting as soon as the host starts stopping, before this service is
    // stopped: from then on Quartz refuses to rebuild a scheduler, and a try made then would only fail.
    private readonly CancellationTokenSource _stopping =
        CancellationTokenSource.CreateLinkedTokenSource(
            services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    private IScheduler? _scheduler;
    private Task? _deferredStart;
    private int _contendedTries;
    private int _unreachableTries;
    private int _unreachableTriesSinceLogged;
    private OutageKind? _loggedOutage;

    private enum StartResult
    {
        Started,
        Unreachable,
        Contended,
    }

    // What an outage at the start is, each with its own message.
    private enum OutageKind
    {
        Unreachable,
        CannotOpen,
        ReadOnly,
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WarnIfLockOnInsertIsIgnored();
        var result = await TryStartSchedulerAsync(cancellationToken);
        if (result == StartResult.Started)
        {
            return;
        }

        _deferredStart = StartWhenReadyAsync(result, _stopping.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        await (_deferredStart ?? Task.CompletedTask);

        try
        {
            await ShutdownSchedulerAsync(cancellationToken);
        }
        finally
        {
            // Only now, with the scheduler no longer listening, are the jobs still running told to stop: they record
            // nothing, and the firing the scheduler abandoned is what runs them again. Told even when the shutdown
            // threw: a handler never told would run on as if its host were still up.
            shutdownSignal.Raise();
        }
    }

    /// <summary>
    /// Cancels first, so a deferred start still waiting for the database ends even when the host is disposed
    /// without having been stopped, for example after another hosted service failed to start.
    /// </summary>
    public void Dispose()
    {
        // Stopping has usually cancelled already; the check also keeps a second Dispose from throwing.
        if (!_stopping.IsCancellationRequested)
        {
            _stopping.Cancel();
        }

        _stopping.Dispose();
    }

    /// <summary>
    /// Running jobs get a bounded time to finish and record their outcome. When it is up the scheduler lets go of
    /// the rest, which leaves them for recovery on the next node to check in.
    /// </summary>
    private async Task ShutdownSchedulerAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is null)
        {
            return;
        }

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(TimeSpan.FromSeconds(options.Value.ShutdownWaitSeconds));
        await _scheduler.Shutdown(waitForJobsToComplete: true, wait.Token);
    }

    private async Task StartSchedulerAsync(CancellationToken cancellationToken)
    {
        // The factory checks the snapshot requirement before it builds the scheduler, so a node refused here never
        // acquires a trigger.
        var scheduler = await services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);

        // Kept before anything else can fail or be cancelled: the factory caches the scheduler it built, so a
        // stop that arrives mid-registration must still find it to shut it down.
        _scheduler = scheduler;

        await StoreDurableJobsAsync(scheduler, cancellationToken);
        if (options.Value.RunInProcess)
        {
            await scheduler.Start(cancellationToken);
        }
    }

    // Every host with a handler keeps its job type's durable job in the store, so a trigger enqueued anywhere has a
    // job to point at.
    private async Task StoreDurableJobsAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        foreach (var jobType in registry.JobTypes)
        {
            await scheduler.AddJob(
                QuartzRegistration.DurableJobFor(jobType),
                AddJobOptions.Replacing,
                cancellationToken);
        }
    }

    /// <summary>
    /// Starts the scheduler, or says why not when it is worth trying again: the database is away, before the start
    /// or during it, or it lost a race with another node's transaction, such as two nodes storing their durable jobs
    /// at once. A failure the database refused with is thrown.
    /// </summary>
    private async Task<StartResult> TryStartSchedulerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await database.UnreachableBecauseAsync(cancellationToken) is { } away)
            {
                return Unreachable(away);
            }

            await StartSchedulerAsync(cancellationToken);
            return StartResult.Started;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsWorthRetrying(exception))
        {
            return await RetryAfterAsync(exception, cancellationToken);
        }
    }

    // Quartz's store and the snapshot check take LockOnInsert from configuration at registration; a host that sets it
    // in code (Configure or PostConfigure) is told its value does not apply rather than left guessing. A warning, not
    // a failed start: the store and the check agree on the value in effect, so the node is safe either way.
    private void WarnIfLockOnInsertIsIgnored()
    {
        if (options.Value.LockOnInsert is { } inOptions && inOptions != database.LocksOnInsert)
        {
            logger.LogWarning(
                "Endatix:BackgroundJobs:LockOnInsert is {InOptions} in the options but {InEffect} in the scheduler's store, which reads it from configuration at registration. Configuration wins: a value set in code is ignored.",
                inOptions,
                database.LocksOnInsert);
        }
    }

    private static bool IsWorthRetrying(Exception exception) =>
        DatabaseFailures.IsContention(exception) || DatabaseFailures.IsTimeout(exception)
        || DatabaseFailures.IsAwayAtStartup(exception) || DatabaseFailures.IsAbortedTransaction(exception);

    // Why a failed start is worth trying again. What arrives here failed after the probe connected, but Quartz opens
    // connections of its own while it starts: a timeout while connecting is the network, and any other timeout is a
    // lock wait, contention, unless the database went away meanwhile, which asking the probe again tells. A refusal
    // the probe meets is thrown.
    private async Task<StartResult> RetryAfterAsync(Exception exception, CancellationToken cancellationToken)
    {
        if (DatabaseFailures.IsContention(exception))
        {
            return Contended(exception);
        }

        return DatabaseFailures.IsAbortedTransaction(exception)
            ? await ExplainAbortedTransactionAsync(exception, cancellationToken)
            : await RetryAfterOutageAsync(exception, cancellationToken);
    }

    private async Task<StartResult> RetryAfterOutageAsync(Exception exception, CancellationToken cancellationToken)
    {
        // A timeout while connecting is the network, not a lock: a connection Quartz opened timed out in a blip.
        if (!DatabaseFailures.IsTimeout(exception) || DatabaseFailures.FailedToConnect(exception))
        {
            return Unreachable(exception);
        }

        return await database.UnreachableBecauseAsync(cancellationToken) is { } away ? Unreachable(away) : Contended(exception);
    }

    // A 25P02 hides why its transaction failed, such as a connection that landed on a read-only session during a
    // switchover after the probe found a writable one, so the probe is asked again; a failure it cannot explain stays a
    // refusal.
    private async Task<StartResult> ExplainAbortedTransactionAsync(Exception aborted, CancellationToken cancellationToken)
    {
        if (await database.UnreachableBecauseAsync(cancellationToken) is { } explained)
        {
            return Unreachable(explained);
        }

        ExceptionDispatchInfo.Throw(aborted);
        return StartResult.Started; // Not reached: Throw does not return.
    }

    private StartResult Contended(Exception lostRace)
    {
        if (++_contendedTries % TriesPerRepeatedWarning == 1)
        {
            logger.LogWarning(
                lostRace,
                "The background jobs scheduler lost a race with another transaction while starting, a deadlock or a wait on a lock that timed out, and will try again ({Tries} tries so far).",
                _contendedTries);
        }

        return StartResult.Contended;
    }

    private async Task StartWhenReadyAsync(StartResult result, CancellationToken stoppingToken)
    {
        try
        {
            await RetryUntilStartedAsync(result, stoppingToken);
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // The host stopped before the database came back; a try that failed because it is stopping, as Quartz
            // refusing to rebuild the scheduler does, is part of the stop.
        }
        catch (Exception exception)
        {
            // Startup has already finished, so there is no one left to fail; say it as loudly as possible.
            logger.LogCritical(exception, "The background jobs scheduler could not start; this host runs no jobs.");
        }
    }

    // Waits after each failed try for as long as its kind of failure takes to clear, then tries again.
    private async Task RetryUntilStartedAsync(StartResult result, CancellationToken stoppingToken)
    {
        do
        {
            await Task.Delay(DelayAfter(result), stoppingToken);
            result = await TryStartSchedulerAsync(stoppingToken);
        }
        while (result != StartResult.Started);
    }

    // Logged with the driver's failure, on the first try and then one in as many as a lost race is: only the operator
    // can tell an outage from a mistake, such as a mistyped host. A database that cannot be opened is named too, since
    // SQL Server reports a misnamed one as it reports a restoring one. Drivers leave the password out of what they
    // report, so the failure is safe to log.
    // An outage of a different kind from the one last logged is logged at once, so a change, from unreachable to
    // read-only say, is not held back behind the other's cadence.
    private StartResult Unreachable(Exception away)
    {
        _contendedTries = 0;
        _unreachableTries++;
        var kind = KindOf(away);
        if (kind != _loggedOutage || ++_unreachableTriesSinceLogged >= TriesPerRepeatedWarning)
        {
            LogUnreachable(away, kind);
            _loggedOutage = kind;
            _unreachableTriesSinceLogged = 0;
        }

        return StartResult.Unreachable;
    }

    private static OutageKind KindOf(Exception away)
    {
        if (DatabaseFailures.IsDatabaseUnavailable(away))
        {
            return OutageKind.CannotOpen;
        }

        return DatabaseFailures.IsReadOnly(away) ? OutageKind.ReadOnly : OutageKind.Unreachable;
    }

    private void LogUnreachable(Exception away, OutageKind kind)
    {
        switch (kind)
        {
            case OutageKind.CannotOpen:
                LogCannotOpen(away);
                break;
            case OutageKind.ReadOnly:
                LogReadOnly(away);
                break;
            default:
                logger.LogError(
                    away,
                    "The background jobs scheduler could not reach its database and will start once it can; until then this host runs no jobs ({Tries} tries so far).",
                    _unreachableTries);
                break;
        }
    }

    private void LogCannotOpen(Exception away) =>
        logger.LogError(
            away,
            "The background jobs scheduler cannot open database '{Database}': it is missing, offline or being restored, or not created yet. It will start once it can; until then this host runs no jobs ({Tries} tries so far).",
            database.DatabaseName,
            _unreachableTries);

    // Read-only on purpose never clears by waiting, so the log says it may be either.
    private void LogReadOnly(Exception away) =>
        logger.LogError(
            away,
            "The background jobs scheduler cannot write to database '{Database}': it is read-only, as during a failover, or set read-only on purpose. It will start once it can write; until then this host runs no jobs ({Tries} tries so far).",
            database.DatabaseName,
            _unreachableTries);

    private TimeSpan DelayAfter(StartResult result) =>
        result == StartResult.Contended && _contendedTries <= QuickContendedTries ? ContendedRetryDelay : UnreachableRetryDelay;
}
