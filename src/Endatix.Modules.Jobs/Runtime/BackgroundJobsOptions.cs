using Ardalis.GuardClauses;

namespace Endatix.Modules.Jobs.Runtime;

/// <remarks>
/// <see cref="JobTypes"/> overrides <see cref="MaxAttempts"/>, <see cref="MaxRuntimeMinutes"/>,
/// <see cref="BackoffBaseSeconds"/> and <see cref="BackoffCapSeconds"/> one value at a time, so the runtime must
/// read those four through <see cref="ResolvePolicy"/>; reading the property itself ignores every override.
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Endatix:BackgroundJobs";

    private Dictionary<string, BackgroundJobTypeOptions> _jobTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every host that registers the module can enqueue, so set this to <c>false</c> on a host that should only
    /// enqueue, such as an API tier deployed apart from its workers.
    /// </summary>
    public bool RunInProcess { get; set; } = true;

    public int MaxConcurrency { get; set; } = 4;

    public int SweepIntervalSeconds { get; set; } = 10;

    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// Minutes without a heartbeat after which a running job is presumed lost and handed back for another
    /// attempt. Must span at least three <see cref="HeartbeatIntervalSeconds"/>.
    /// </summary>
    public int StuckJobThresholdMinutes { get; set; } = 10;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int MaxRuntimeMinutes { get; set; } = 60;

    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Seconds before the first retry. Each later retry waits twice as long as the one before it, up to
    /// <see cref="BackoffCapSeconds"/>.
    /// </summary>
    public int BackoffBaseSeconds { get; set; } = 30;

    public int BackoffCapSeconds { get; set; } = 900;

    /// <summary>
    /// Minutes an eligible job may wait without being claimed before the sweeper warns about a backlog. Unlike
    /// <see cref="StuckJobThresholdMinutes"/>, which measures a lost heartbeat, this measures time spent waiting
    /// for a runner.
    /// </summary>
    public int BacklogWarningMinutes { get; set; } = 15;

    /// <summary>
    /// Overrides keyed by job type, for example
    /// <c>Endatix:BackgroundJobs:JobTypes:SubmissionExport:MaxAttempts</c>.
    /// </summary>
    /// <remarks>
    /// A key matches its job type regardless of case, even though job types themselves are case-sensitive:
    /// configuration keys are case-insensitive everywhere else, so an override written in a different case has to
    /// apply rather than be silently ignored. Only the four values of <see cref="BackgroundJobTypeOptions"/>
    /// override; anything else written under a job type binds to nothing and is ignored.
    /// </remarks>
    public Dictionary<string, BackgroundJobTypeOptions> JobTypes
    {
        get => _jobTypes;
        set
        {
            Guard.Against.Null(value);

            // Copied into a dictionary that ignores case whatever comparer the caller used, so an assignment
            // whose keys differ only in case throws instead of letting one of them silently win.
            _jobTypes = new Dictionary<string, BackgroundJobTypeOptions>(value, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Resolves the policy for <paramref name="jobType"/>, taking each value from the job type's override when
    /// it is set and from the global value otherwise.
    /// </summary>
    internal BackgroundJobTypePolicy ResolvePolicy(string jobType)
    {
        var overrides = JobTypes.TryGetValue(jobType, out var jobTypeOptions) ? jobTypeOptions : null;

        return new BackgroundJobTypePolicy(
            MaxAttempts: overrides?.MaxAttempts ?? MaxAttempts,
            MaxRuntime: TimeSpan.FromMinutes(overrides?.MaxRuntimeMinutes ?? MaxRuntimeMinutes),
            BackoffBase: TimeSpan.FromSeconds(overrides?.BackoffBaseSeconds ?? BackoffBaseSeconds),
            BackoffCap: TimeSpan.FromSeconds(overrides?.BackoffCapSeconds ?? BackoffCapSeconds));
    }
}

/// <summary>
/// Overrides for one job type, bound from <c>Endatix:BackgroundJobs:JobTypes:{JobType}</c>. A value left unset
/// falls back to the global value of the same name on <see cref="BackgroundJobsOptions"/>.
/// </summary>
public sealed class BackgroundJobTypeOptions
{
    public int? MaxAttempts { get; set; }

    public int? MaxRuntimeMinutes { get; set; }

    public int? BackoffBaseSeconds { get; set; }

    public int? BackoffCapSeconds { get; set; }
}

/// <summary>
/// The retry and runtime values in effect for one job type, once its overrides have fallen back to the global
/// values.
/// </summary>
internal readonly record struct BackgroundJobTypePolicy(
    int MaxAttempts,
    TimeSpan MaxRuntime,
    TimeSpan BackoffBase,
    TimeSpan BackoffCap);
