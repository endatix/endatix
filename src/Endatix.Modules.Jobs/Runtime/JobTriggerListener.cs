using System.Diagnostics.Metrics;
using System.Globalization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Takes over a job whose trigger ran out of scheduler retries while the job's row was unfinished.
/// </summary>
/// <remarks>
/// <para>
/// Only a trigger stored by an earlier version carries a scheduler retry policy, and its retries run out only when a
/// firing throws to the scheduler, which the job wrapper never means to do. The scheduler then deletes the trigger,
/// and a row it leaves unfinished would never run again, so the job is re-fired to be taken over, and the
/// disagreement is logged and counted.
/// </para>
/// <para>
/// The re-fire is a trigger keyed by the job's id, as every trigger of the job is, so it replaces whatever trigger
/// the job has rather than adding a second, however many nodes run this; and its claim is fenced on the row's
/// attempt, so a take-over the row did not need changes nothing.
/// </para>
/// </remarks>
internal sealed class JobTriggerListener : ITriggerListener
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobTriggerListener> _logger;
    private readonly Counter<long> _retriesExhaustedMismatches;

    public JobTriggerListener(
        IServiceScopeFactory scopeFactory,
        IMeterFactory meterFactory,
        ILogger<JobTriggerListener> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retriesExhaustedMismatches = meterFactory.Create(JobsModule.MeterName).CreateCounter<long>(
            "endatix.jobs.retries_exhausted_mismatch",
            unit: "{job}",
            description: "Jobs whose scheduler retries ran out while their row was not yet terminal, each then taken over.");
    }

    public string Name => "endatix-jobs-trigger-listener";

    public async ValueTask TriggerRetriesExhausted(
        ITrigger trigger,
        IJobExecutionContext context,
        JobExecutionException exception,
        CancellationToken cancellationToken = default)
    {
        if (JobIdOf(context) is { } jobId)
        {
            await TakeOverIfUnfinishedAsync(context, new ReclaimableJob(jobId, trigger.JobKey.Name), cancellationToken);
        }
    }

    private async Task TakeOverIfUnfinishedAsync(
        IJobExecutionContext context,
        ReclaimableJob job,
        CancellationToken cancellationToken)
    {
        if (!await ReportIfUnfinishedAsync(job, cancellationToken))
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UnrecordedJobRefire>().TakeOverAsync(context, job);
    }

    // Returns whether the job is to be taken over. A row that cannot be read is: taking over a finished row finds
    // nothing to claim, while leaving an unfinished one would strand it.
    private async Task<bool> ReportIfUnfinishedAsync(ReclaimableJob job, CancellationToken cancellationToken)
    {
        try
        {
            if (await ReadStatusAsync(job.JobId, cancellationToken) is not { } current || IsTerminal(current))
            {
                return false;
            }

            ReportUnfinished(job, current);
            return true;
        }
        catch (Exception readFailure)
        {
            ReportUnread(job, readFailure);
            return true;
        }
    }

    private void ReportUnread(ReclaimableJob job, Exception readFailure) =>
        _logger.LogWarning(
            readFailure,
            "Reading background job {JobId} after its scheduler retries ran out failed; the job is re-fired to be taken over in case its row is unfinished",
            job.JobId);

    private Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken) =>
        _scopeFactory.WithStateRepositoryAsync(repository => repository.ReadStatusAsync(jobId, cancellationToken));

    private void ReportUnfinished(ReclaimableJob job, JobStatus status)
    {
        _retriesExhaustedMismatches.Add(1, new KeyValuePair<string, object?>("endatix.job.type", job.JobType));
        _logger.LogWarning(
            "The scheduler ran out of retries for background job {JobId} of type {JobType}, whose row is still {Status}; the job is re-fired to be taken over",
            job.JobId,
            job.JobType,
            status);
    }

    private static bool IsTerminal(JobStatus status) =>
        status is JobStatus.Completed or JobStatus.Failed or JobStatus.DeadLettered or JobStatus.Canceled;

    private static long? JobIdOf(IJobExecutionContext context) =>
        context.MergedJobDataMap.GetString(BackgroundJobExecution.JobIdKey) is { } value
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var jobId)
            ? jobId
            : null;
}
