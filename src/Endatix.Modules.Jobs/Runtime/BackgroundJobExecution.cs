using Endatix.Core.Abstractions;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The only scheduler job class. One durable scheduler job per job type points at it, and each trigger carries
/// only the id of the job row it fires. It claims the row, runs the handler and records how the attempt ended.
/// </summary>
/// <remarks>
/// <para>
/// Handlers never see this class or any scheduler type: everything scheduler-specific stays in the job wrapper —
/// this class, the firing it reads and the triggers it schedules — so replacing the scheduler rewrites the wrapper
/// and not the handlers.
/// </para>
/// <para>
/// Every write after the claim is fenced on the attempt the claim took, so a run that lost the job — to a
/// cancellation, or to a recovery on another node — changes nothing. The claim, the handler and the outcome each
/// run in a scope of their own, because a <c>DbContext</c> is neither thread-safe nor meant to be held for the
/// length of a job.
/// </para>
/// </remarks>
internal sealed class BackgroundJobExecution(
    JobFiringAdmission admission,
    JobAttemptClaimer claimer,
    JobHandlerRunner runner,
    JobOutcomeRecorder outcomes,
    UnrecordedJobRefire refire,
    IDateTimeProvider dateTimeProvider) : IJob
{
    /// <summary>The trigger data key that holds the job row's id.</summary>
    public const string JobIdKey = "jobId";

    /// <summary>
    /// The trigger data key that marks a firing scheduled to take the job over from a firing that left its row
    /// unsettled, so it re-claims a row left <c>Processing</c>, as a recovery would.
    /// </summary>
    public const string ReclaimKey = "reclaim";

    /// <summary>
    /// Runs one firing. It never throws to the scheduler: every retry runs on a trigger the wrapper schedules for
    /// the job, so a trigger stored with a scheduler retry policy, as earlier versions stored them, ends without
    /// that policy retrying it.
    /// </summary>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var attempt = await AdmitAndClaimAsync(context, cancellationToken);
        if (attempt is null)
        {
            return;
        }

        var run = await runner.RunAsync(attempt, cancellationToken);
        await EndAttemptAsync(context, attempt, run);
    }

    private async Task<ClaimedAttempt?> AdmitAndClaimAsync(IJobExecutionContext context, CancellationToken cancellationToken) =>
        await admission.AdmitAsync(context) is { } firing
            ? await ClaimAsync(context, firing, cancellationToken)
            : null;

    /// <summary>
    /// Claims the row the firing points at. The firing's trigger is the job's only one and is deleted once the
    /// firing completes, so a claim that fails re-fires the job rather than leaving it with nothing to run it.
    /// </summary>
    private async Task<ClaimedAttempt?> ClaimAsync(
        IJobExecutionContext context,
        JobFiring firing,
        CancellationToken cancellationToken)
    {
        try
        {
            return await claimer.ClaimAsync(firing, dateTimeProvider.UtcNow.UtcDateTime, cancellationToken);
        }
        catch (Exception exception) // Any failure: whatever it was, the job would be left without a trigger.
        {
            await refire.RefireUnclaimedAsync(context, new ReclaimableJob(firing.JobId, firing.JobType), exception);
            return null;
        }
    }

    /// <summary>Records how the attempt ended, and re-fires the job when nothing could be recorded.</summary>
    private async Task EndAttemptAsync(IJobExecutionContext context, ClaimedAttempt attempt, HandlerRun run)
    {
        var write = AttemptDecision.Decide(run.End, attempt.Job.AttemptCount, attempt.Policy.MaxAttempts);
        var ending = new AttemptEnding(write, run, NextAttemptAt(attempt));
        if (await RecordAsync(context, attempt, ending) is OutcomeWrite.Unwritable or OutcomeWrite.Unscheduled)
        {
            await refire.RefireAsync(context, attempt);
        }
    }

    /// <summary>
    /// Writes the attempt's outcome, after scheduling the next attempt's trigger when the attempt is to be retried.
    /// That trigger is stored before the row says <c>Retrying</c>: should the write then not land, the trigger finds
    /// a row it cannot claim and does nothing. When the trigger cannot be stored, nothing is written, because a
    /// <c>Retrying</c> row with no trigger is never claimed again; the row stays <c>Processing</c> for a re-fire to
    /// take over.
    /// </summary>
    private async Task<OutcomeWrite> RecordAsync(IJobExecutionContext context, ClaimedAttempt attempt, AttemptEnding ending)
    {
        if (ending.Write is AttemptRowWrite.Retrying
            && !await refire.TryScheduleRetryAsync(context, attempt, ending.NextAttemptAt))
        {
            outcomes.LogUnrecordedThrow(attempt, ending.Run);
            return OutcomeWrite.Unscheduled;
        }

        return await outcomes.RecordAsync(attempt, ending);
    }

    private DateTime NextAttemptAt(ClaimedAttempt attempt) =>
        BackgroundJobRetryPolicy.NextAttemptAt(
            attempt.Job.AttemptCount, dateTimeProvider.UtcNow.UtcDateTime, attempt.Policy);
}
