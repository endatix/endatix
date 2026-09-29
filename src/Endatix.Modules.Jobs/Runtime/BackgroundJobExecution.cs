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
    /// The trigger data key that marks a firing scheduled to take the job over from an attempt whose outcome could
    /// not be written, so it re-claims the row that attempt left <c>Processing</c>, as a recovery would.
    /// </summary>
    public const string ReclaimKey = "reclaim";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var claimedAt = dateTimeProvider.UtcNow.UtcDateTime;
        var attempt = await claimer.ClaimAsync(JobFiring.Of(context), claimedAt, cancellationToken);
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
    /// Records how the attempt ended, and returns whether the scheduler's retry policy is to schedule the next
    /// attempt, which it does when the wrapper throws.
    /// </summary>
    private async Task<bool> EndAttemptAsync(IJobExecutionContext context, ClaimedAttempt attempt, HandlerRun run)
    {
        var decision = AttemptDecision.Decide(run.End, attempt.Job.AttemptCount, attempt.Policy.MaxAttempts);
        var ending = new AttemptEnding(decision.Row, run, NextAttemptAt(attempt));
        var needsOwnTrigger = decision.Rethrow && context.Trigger.RetryPolicy is null;
        if (needsOwnTrigger)
        {
            await ScheduleOwnRetryAsync(context, attempt, ending.NextAttemptAt);
        }

        var written = await outcomes.RecordAsync(attempt, ending);
        if (written is OutcomeWrite.Unwritable)
        {
            await refire.RefireAsync(context, attempt);
            return false;
        }

        return decision.Rethrow && written is OutcomeWrite.Landed && !needsOwnTrigger;
    }

    /// <summary>
    /// A firing Quartz created to recover a dead node's job carries no retry policy, so a rethrow would end it for
    /// good. The next attempt gets a trigger of its own instead, as enqueueing would have made it. It is scheduled
    /// before the row says <c>Retrying</c>: should the row write then not land, the trigger finds a row it cannot
    /// claim and does nothing, where the other order could leave a <c>Retrying</c> row with no trigger.
    /// </summary>
    private static async Task ScheduleOwnRetryAsync(
        IJobExecutionContext context,
        ClaimedAttempt attempt,
        DateTime nextAttemptAt)
    {
        var job = attempt.Job;
        await context.Scheduler.ScheduleJob(
            QuartzRegistration.TriggerFor(
                new JobTriggerSpec(job.Id, job.JobType, attempt.Policy, new DateTimeOffset(nextAttemptAt))),
            ScheduleJobOptions.Replacing,
            CancellationToken.None);
    }

    private DateTime NextAttemptAt(ClaimedAttempt attempt) =>
        BackgroundJobRetryPolicy.NextAttemptAt(
            attempt.Job.AttemptCount, dateTimeProvider.UtcNow.UtcDateTime, attempt.Policy);
}
