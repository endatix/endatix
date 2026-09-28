using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The backlog warning: a trigger that waited past the misfire threshold, because every slot of its job type was
/// busy or no running node has the job type's handler.
/// </summary>
internal sealed class JobMisfireListener : ITriggerListener
{
    private readonly ILogger<JobMisfireListener> _logger;
    private readonly Counter<long> _misfired;

    public JobMisfireListener(IMeterFactory meterFactory, ILogger<JobMisfireListener> logger)
    {
        _logger = logger;
        _misfired = meterFactory.Create(JobsModule.MeterName).CreateCounter<long>(
            "endatix.jobs.misfired",
            unit: "{trigger}",
            description: "Job triggers that waited past the misfire threshold.");
    }

    public string Name => "endatix-jobs-misfire-listener";

    public ValueTask TriggerMisfired(ITrigger trigger, IScheduler scheduler, CancellationToken cancellationToken = default)
    {
        var jobType = trigger.JobKey.Name;
        _misfired.Add(1, new KeyValuePair<string, object?>("job_type", jobType));
        _logger.LogWarning(
            "Background job {JobId} of type {JobType} waited past the misfire threshold.",
            trigger.Key.Name,
            jobType);

        return default;
    }
}
