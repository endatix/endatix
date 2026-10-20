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
    private int _unexplainedAborts;
    private int _unreachableTries;
    private readonly Dictionary<OutageKind, int> _triesSinceLogged = [];

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

    // A 25P02 hides why its transaction failed: Quartz 4.2.2's row-lock handler retries its lock statement inside the
    // aborted transaction and reports only the last 25P02 (measured). So the database is asked. A connection that
    // landed on a read-only session during a switchover, after the probe found a writable one, is an outage. A role
    // that may not take the lock (42501, hidden the same way, measured) is a refusal, and fails the start as a refused
    // login does. Anything else is a lost race inside the lock, such as a lock wait past lock_timeout (55P03, measured).
    // A missing lock table never gets this far: Quartz's schema validation refuses the start first (measured).
    private async Task<StartResult> ExplainAbortedTransactionAsync(Exception aborted, CancellationToken cancellationToken)
    {
        return await database.UnreachableBecauseAsync(cancellationToken) is { } explained
            ? Unreachable(explained)
            : await RetryUnlessTheLockIsRefusedAsync(aborted, cancellationToken);
    }

    // A role that may not take the lock fails the start. A check that could not be made because the database went away,
    // or waited too long on a statement_timeout or the command timeout, is no refusal; one it was refused, such as a
    // 42501 without USAGE on the schema, still fails the start.
    private async Task<StartResult> RetryUnlessTheLockIsRefusedAsync(Exception aborted, CancellationToken cancellationToken)
    {
        try
        {
            return await database.LockTableRefusalAsync(cancellationToken) is { } refusal
                ? throw new InvalidOperationException(refusal, aborted)
                : UnexplainedAbort(aborted);
        }
        catch (Exception away) when (DatabaseFailures.IsAwayAtStartup(away))
        {
            return Unreachable(away);
        }
        catch (Exception waited) when (DatabaseFailures.IsTimeout(waited) || DatabaseFailures.IsContention(waited))
        {
            return UnexplainedAbort(aborted);
        }
    }

    // Paced as a lost race; a run of them is raised to an error, since contention that never clears, such as a lock
    // held for long or a lock_timeout shorter than the waits, needs someone to look.
    private StartResult UnexplainedAbort(Exception aborted)
    {
        _contendedTries++;
        var inARow = ++_unexplainedAborts;
        if (inARow % TriesPerRepeatedWarning == 0)
        {
            logger.LogError(
                aborted,
                "The background jobs scheduler has met a failed transaction inside Quartz's lock on {Tries} tries in a row, which Quartz reports without its cause. The role may take the lock, so it is contention that does not clear: a lock on jobs.qrtz_LOCKS held for long, or a lock_timeout or statement_timeout shorter than the waits. Retrying.",
                inARow);
        }
        else if (inARow == 1)
        {
            logger.LogWarning(
                aborted,
                "The background jobs scheduler met a failed transaction inside Quartz's lock while starting, which Quartz reports without its cause; the role may take the lock, so it is a lost race, such as a lock wait past lock_timeout. Retrying.");
        }

        return StartResult.Contended;
    }

    private StartResult Contended(Exception lostRace)
    {
        _unexplainedAborts = 0;
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
        catch (Exception exception) when (stoppingToken.IsCancellationRequested)
        {
            WarnUnlessPartOfTheStop(exception);
        }
        catch (Exception exception)
        {
            // Startup has already finished, so there is no one left to fail; say it as loudly as possible.
            logger.LogCritical(exception, "The background jobs scheduler could not start; this host runs no jobs.");
        }
    }

    // The host stopped before the database came back. A failure the stop itself causes says nothing; a refusal that
    // arrived as the host began to stop still says why this host ran no jobs.
    private void WarnUnlessPartOfTheStop(Exception exception)
    {
        if (!IsPartOfTheStop(exception))
        {
            logger.LogWarning(exception, "The background jobs scheduler could not start before the host stopped; this host ran no jobs.");
        }
    }

    // What a stop itself causes is a cancellation: the wait cancelled, or a rebuild the scheduler's factory turned into
    // one because the host is stopping, which the factory logs at Debug. A refusal raised outside the rebuild, such as
    // the snapshot check's, is not a cancellation, and is logged at Warning.
    private static bool IsPartOfTheStop(Exception exception) => exception.HasCause(cause => cause is OperationCanceledException);

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
    // Each kind of outage keeps its own cadence: one met for the first time is logged at once, so a change, from
    // unreachable to read-only say, is not held back behind the other's, and kinds that alternate, as during a
    // failover, are each logged one try in twelve rather than every time.
    private StartResult Unreachable(Exception away)
    {
        _contendedTries = 0;
        _unexplainedAborts = 0;
        _unreachableTries++;
        var kind = KindOf(away);
        var since = _triesSinceLogged.TryGetValue(kind, out var tries) ? tries + 1 : TriesPerRepeatedWarning;
        if (since >= TriesPerRepeatedWarning)
        {
            LogUnreachable(away, kind);
            since = 0;
        }

        _triesSinceLogged[kind] = since;

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
