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

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var attempt = await ClaimAsync(context, JobFiring.Of(context), cancellationToken);
        if (attempt is null)
        {
            // Returning tells the scheduler this firing is done.
            return;
        }

        var run = await runner.RunAsync(attempt, cancellationToken);
        if (await EndAttemptAsync(context, attempt, run))
        {
            // No inner exception: it was logged when its message was resolved, and the scheduler would log it
            // again.
            throw new JobExecutionException(
                $"Background job {attempt.Job.Id} attempt {attempt.Job.AttemptCount} failed and will be retried.");
        }
    }

    /// <summary>
    /// Claims the row the firing points at. A firing that takes the job over and carries no retry policy is the
    /// job's only trigger, and a throw would end it for good, so a claim that fails re-fires the job instead; a
    /// firing with a policy throws, and the scheduler retries it.
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
        catch (Exception exception) when (firing.Reclaiming && context.Trigger.RetryPolicy is null) // Any failure: the firing is the job's only trigger.
        {
            var job = new ReclaimableJob(firing.JobId, firing.JobType, claimer.PolicyFor(firing.JobType));
            await refire.RefireUnclaimedAsync(context, job, exception);
            return null;
        }
    }

    /// <summary>
    /// Records how the attempt ended, and returns whether the scheduler's retry policy is to schedule the next
    /// attempt, which it does when the wrapper throws.
    /// </summary>
    private async Task<bool> EndAttemptAsync(IJobExecutionContext context, ClaimedAttempt attempt, HandlerRun run)
    {
        var decision = AttemptDecision.Decide(run.End, attempt.Job.AttemptCount, attempt.Policy.MaxAttempts);
        var ending = new AttemptEnding(decision.Row, run, NextAttemptAt(attempt));
        var written = await RecordAsync(context, attempt, ending);
        if (written is OutcomeWrite.Unwritable or OutcomeWrite.Unscheduled)
        {
            await refire.RefireAsync(context, attempt);
            return false;
        }

        return decision.Rethrow && written is OutcomeWrite.Landed && !NeedsOwnTrigger(context, ending);
    }

    /// <summary>
    /// Writes the attempt's outcome, after scheduling the next attempt's trigger when the firing cannot retry. That
    /// trigger is stored before the row says <c>Retrying</c>: should the write then not land, the trigger finds a row
    /// it cannot claim and does nothing. When the trigger cannot be stored, nothing is written, because a
    /// <c>Retrying</c> row with no trigger is never claimed again; the row stays <c>Processing</c> for a re-fire to
    /// take over.
    /// </summary>
    private async Task<OutcomeWrite> RecordAsync(IJobExecutionContext context, ClaimedAttempt attempt, AttemptEnding ending)
    {
        if (NeedsOwnTrigger(context, ending)
            && !await refire.TryScheduleRetryAsync(context, attempt, ending.NextAttemptAt))
        {
            outcomes.LogUnrecordedThrow(attempt, ending.Run);
            return OutcomeWrite.Unscheduled;
        }

        return await outcomes.RecordAsync(attempt, ending);
    }

    /// <summary>
    /// A firing Quartz created to recover a dead node's job carries no retry policy, so a rethrow would end it for
    /// good. The next attempt gets a trigger of its own instead, as enqueueing would have made it.
    /// </summary>
    private static bool NeedsOwnTrigger(IJobExecutionContext context, AttemptEnding ending) =>
        ending.Write is AttemptRowWrite.Retrying && context.Trigger.RetryPolicy is null;

    private DateTime NextAttemptAt(ClaimedAttempt attempt) =>
        BackgroundJobRetryPolicy.NextAttemptAt(
            attempt.Job.AttemptCount, dateTimeProvider.UtcNow.UtcDateTime, attempt.Policy);
}
