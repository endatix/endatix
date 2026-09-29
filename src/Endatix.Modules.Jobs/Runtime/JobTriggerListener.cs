using System.Diagnostics.Metrics;
using System.Globalization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Keeps the job row in step with what the scheduler decided about its trigger.
/// </summary>
/// <remarks>
/// After a retry is scheduled it mirrors the trigger's next fire time to <c>NextAttemptAt</c>, so the status
/// endpoint shows when the next attempt is due. When the scheduler's retry budget runs out it checks that the row
/// agrees; the row, not the scheduler, decides dead-lettering, so a disagreement is logged and counted, never
/// acted on.
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
            description: "Jobs whose scheduler retries ran out while their row was not yet terminal.");
    }

    public string Name => "endatix-jobs-trigger-listener";

    public async ValueTask TriggerComplete(
        ITrigger trigger,
        IJobExecutionContext context,
        SchedulerInstruction triggerInstructionCode,
        CancellationToken cancellationToken = default)
    {
        if (triggerInstructionCode == SchedulerInstruction.RetryTrigger
            && JobIdOf(context) is { } jobId
            && trigger.NextFireTimeUtc is { } nextFireTime)
        {
            await MirrorNextAttemptAsync(jobId, nextFireTime.UtcDateTime, cancellationToken);
        }
    }

    public async ValueTask TriggerRetriesExhausted(
        ITrigger trigger,
        IJobExecutionContext context,
        JobExecutionException exception,
        CancellationToken cancellationToken = default)
    {
        if (JobIdOf(context) is { } jobId)
        {
            await CheckRowIsTerminalAsync(jobId, trigger.JobKey.Name, cancellationToken);
        }
    }

    private async Task MirrorNextAttemptAsync(long jobId, DateTime nextAttemptAt, CancellationToken cancellationToken)
    {
        try
        {
            await _scopeFactory.WithStateRepositoryAsync(
                repository => repository.TryMirrorNextAttemptAsync(jobId, nextAttemptAt, cancellationToken));
        }
        catch (Exception exception)
        {
            // The column is for display; the retry is already scheduled whatever it says.
            _logger.LogWarning(exception, "Recording the next attempt time of background job {JobId} failed", jobId);
        }
    }

    private async Task CheckRowIsTerminalAsync(long jobId, string jobType, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _scopeFactory.WithStateRepositoryAsync(
                repository => repository.ReadStatusAsync(jobId, cancellationToken));
            if (status is not (null or JobStatus.Completed or JobStatus.Failed or JobStatus.DeadLettered or JobStatus.Canceled))
            {
                ReportMismatch(jobId, jobType, status.Value);
            }
        }
        catch (Exception readFailure)
        {
            _logger.LogWarning(readFailure, "Checking background job {JobId} after its retries ran out failed", jobId);
        }
    }

    private void ReportMismatch(long jobId, string jobType, JobStatus status)
    {
        _retriesExhaustedMismatches.Add(1, new KeyValuePair<string, object?>("endatix.job.type", jobType));
        _logger.LogWarning(
            "The scheduler ran out of retries for background job {JobId} of type {JobType}, whose row is still {Status}",
            jobId,
            jobType,
            status);
    }

    private static long? JobIdOf(IJobExecutionContext context) =>
        context.MergedJobDataMap.GetString(BackgroundJobExecution.JobIdKey) is { } value
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var jobId)
            ? jobId
            : null;
}
