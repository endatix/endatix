using Ardalis.GuardClauses;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Options for executing background jobs, bound from the <c>Endatix:BackgroundJobs</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MaxAttempts"/>, <see cref="MaxRuntimeMinutes"/>, <see cref="BackoffBaseSeconds"/> and
/// <see cref="BackoffCapSeconds"/> apply to every job type unless <see cref="JobTypes"/> overrides them, one
/// value at a time. The runtime resolves these four values per job type, so reading one of these properties
/// directly ignores any override in <see cref="JobTypes"/>.
/// </para>
/// <para>
/// Every other value describes the process that runs jobs rather than a kind of job, so it is global only.
/// </para>
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Endatix:BackgroundJobs";

    private Dictionary<string, BackgroundJobTypeOptions> _jobTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this process executes jobs. Every host that registers the module can enqueue, so set this to
    /// <c>false</c> on a host that should only enqueue, such as an API tier deployed apart from its workers.
    /// </summary>
    public bool RunInProcess { get; set; } = true;

    /// <summary>
    /// Maximum number of jobs this process executes at the same time.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Seconds between sweeper passes.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of jobs a sweeper pass reads at each of its steps.
    /// </summary>
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// Minutes without a heartbeat after which a running job is presumed lost and handed back for another
    /// attempt. Must span at least three <see cref="HeartbeatIntervalSeconds"/>.
    /// </summary>
    public int StuckJobThresholdMinutes { get; set; } = 10;

    /// <summary>
    /// Seconds between heartbeats of a running job.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Minutes one attempt may run before it is stopped and recorded as a retryable failure. Can be
    /// overridden per job type.
    /// </summary>
    public int MaxRuntimeMinutes { get; set; } = 60;

    /// <summary>
    /// Attempts a job gets before a retryable failure dead-letters it. Can be overridden per job type.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Seconds before the first retry. Each later retry waits twice as long as the one before it, up to
    /// <see cref="BackoffCapSeconds"/>. Can be overridden per job type.
    /// </summary>
    public int BackoffBaseSeconds { get; set; } = 30;

    /// <summary>
    /// Longest wait between retries, in seconds. Can be overridden per job type.
    /// </summary>
    public int BackoffCapSeconds { get; set; } = 900;

    /// <summary>
    /// Minutes an eligible job may wait without being claimed before the sweeper warns about a backlog. Unlike
    /// <see cref="StuckJobThresholdMinutes"/>, which measures a lost heartbeat, this measures time spent waiting
    /// for a runner. Applies to every job type; there is no per-type override.
    /// </summary>
    public int BacklogWarningMinutes { get; set; } = 15;

    /// <summary>
    /// Overrides keyed by job type, for example
    /// <c>Endatix:BackgroundJobs:JobTypes:SubmissionExport:MaxAttempts</c>. A value left unset falls back to
    /// the global value of the same name.
    /// </summary>
    /// <remarks>
    /// A key matches its job type regardless of case, even though job types themselves are case-sensitive.
    /// Configuration keys are case-insensitive everywhere else, so an override written in a different case has
    /// to apply rather than be silently ignored. An assigned dictionary is copied into one that ignores case,
    /// whatever comparer it was created with, so assigning a dictionary whose keys differ only in case throws.
    /// </remarks>
    public Dictionary<string, BackgroundJobTypeOptions> JobTypes
    {
        get => _jobTypes;
        set
        {
            Guard.Against.Null(value);

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
    /// <summary>
    /// Overrides <see cref="BackgroundJobsOptions.MaxAttempts"/> for this job type.
    /// </summary>
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// Overrides <see cref="BackgroundJobsOptions.MaxRuntimeMinutes"/> for this job type.
    /// </summary>
    public int? MaxRuntimeMinutes { get; set; }

    /// <summary>
    /// Overrides <see cref="BackgroundJobsOptions.BackoffBaseSeconds"/> for this job type.
    /// </summary>
    public int? BackoffBaseSeconds { get; set; }

    /// <summary>
    /// Overrides <see cref="BackgroundJobsOptions.BackoffCapSeconds"/> for this job type.
    /// </summary>
    public int? BackoffCapSeconds { get; set; }
}

/// <summary>
/// The retry and runtime values in effect for one job type, once its overrides have fallen back to the global
/// values.
/// </summary>
/// <param name="MaxAttempts">Attempts before a retryable failure dead-letters the job.</param>
/// <param name="MaxRuntime">How long one attempt may run before it is stopped.</param>
/// <param name="BackoffBase">Wait before the first retry.</param>
/// <param name="BackoffCap">Longest wait between retries.</param>
internal readonly record struct BackgroundJobTypePolicy(
    int MaxAttempts,
    TimeSpan MaxRuntime,
    TimeSpan BackoffBase,
    TimeSpan BackoffCap);
