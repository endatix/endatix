using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The backlog warning: a trigger that waited past the misfire threshold, because every slot of its job type was
/// busy or no running node has the job type's handler.
/// </summary>
/// <remarks>
/// A trigger still waiting misfires again at every threshold, so a backlog of N jobs misfires N times a threshold.
/// The counter counts every misfire, while the warning is written at most once per job type per threshold and says
/// how many misfires it stands for.
/// </remarks>
internal sealed class JobMisfireListener : ITriggerListener
{
    private readonly ILogger<JobMisfireListener> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TimeSpan _warningInterval;
    private readonly Counter<long> _misfired;
    private readonly ConcurrentDictionary<string, WarningWindow> _windows = new(StringComparer.Ordinal);

    public JobMisfireListener(
        IMeterFactory meterFactory,
        IDateTimeProvider dateTimeProvider,
        IOptions<BackgroundJobsOptions> options,
        ILogger<JobMisfireListener> logger)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _warningInterval = TimeSpan.FromSeconds(options.Value.MisfireThresholdSeconds);
        _misfired = meterFactory.Create(JobsModule.MeterName).CreateCounter<long>(
            "endatix.jobs.misfired",
            unit: "{trigger}",
            description: "Job triggers that waited past the misfire threshold.");
    }

    public string Name => "endatix-jobs-misfire-listener";

    public ValueTask TriggerMisfired(ITrigger trigger, IScheduler scheduler, CancellationToken cancellationToken = default)
    {
        // Maintenance such as retention is not a job type and has no backlog; its late run is not this warning.
        if (trigger.JobKey.Group != QuartzRegistration.JobGroup)
        {
            return default;
        }

        var jobType = trigger.JobKey.Name;
        _misfired.Add(1, new KeyValuePair<string, object?>("job_type", jobType));
        WarnOncePerThreshold(trigger, jobType);
        return default;
    }

    private void WarnOncePerThreshold(ITrigger trigger, string jobType)
    {
        var window = _windows.GetOrAdd(jobType, _ => new WarningWindow());
        if (window.TryReport(_dateTimeProvider.UtcNow, _warningInterval, out var misfires))
        {
            _logger.LogWarning(
                "Background job {JobId} of type {JobType} waited past the misfire threshold; {Misfires} triggers of this type misfired since the last warning.",
                trigger.Key.Name,
                jobType,
                misfires);
        }
    }

    /// <summary>The misfires of one job type since its last warning, and when that warning was written.</summary>
    private sealed class WarningWindow
    {
        private readonly Lock _lock = new();
        private DateTimeOffset? _lastReported;
        private long _unreported;

        public bool TryReport(DateTimeOffset now, TimeSpan interval, out long misfires)
        {
            lock (_lock)
            {
                _unreported++;
                if (_lastReported is { } last && now - last < interval)
                {
                    misfires = 0;
                    return false;
                }

                misfires = _unreported;
                _unreported = 0;
                _lastReported = now;
                return true;
            }
        }
    }
}
