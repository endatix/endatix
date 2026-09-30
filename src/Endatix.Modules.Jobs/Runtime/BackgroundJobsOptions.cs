using Ardalis.GuardClauses;

namespace Endatix.Modules.Jobs.Runtime;

/// <remarks>
/// <see cref="JobTypes"/> overrides the per-job-type values one at a time, so the runtime must read them through
/// <see cref="ResolvePolicy"/>; reading the property itself ignores every override.
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Endatix:BackgroundJobs";

    /// <summary>
    /// The concurrency cap of a job type that sets none of its own.
    /// </summary>
    public const int DefaultJobTypeMaxConcurrency = 1;

    private Dictionary<string, BackgroundJobTypeOptions> _jobTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this host executes jobs. Every host that registers the module can enqueue, so set this to
    /// <c>false</c> on a host that should only enqueue, such as an API tier deployed apart from its workers.
    /// </summary>
    public bool RunInProcess { get; set; } = true;

    /// <summary>
    /// How long an idle executing node waits before looking for jobs another node scheduled. Short, because a
    /// job enqueued on a schedule-only host waits at most this long before an executing node sees it.
    /// </summary>
    public int IdleWaitTimeSeconds { get; set; } = 2;

    /// <summary>
    /// How often a running job re-reads its row to notice that it was cancelled.
    /// </summary>
    public int CancellationPollSeconds { get; set; } = 10;

    /// <summary>
    /// How long a stopping host waits for running jobs to finish. A job still running when the wait ends is left
    /// as it is, with nothing recorded, and runs again on the next node to check in. The host's own shutdown timeout
    /// (<c>HostOptions.ShutdownTimeout</c>, 30 seconds by default) also bounds the wait, so a longer value needs a
    /// longer host timeout to take effect.
    /// </summary>
    public int ShutdownWaitSeconds { get; set; } = 30;

    public int MaxRuntimeMinutes { get; set; } = 60;

    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Seconds before the first retry. Each later retry waits twice as long as the one before it, up to
    /// <see cref="BackoffCapSeconds"/>.
    /// </summary>
    public int BackoffBaseSeconds { get; set; } = 30;

    public int BackoffCapSeconds { get; set; } = 900;

    /// <summary>
    /// Days a finished job's row is kept before the retention job may delete it.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// How nodes sharing the job store prove to each other that they are alive.
    /// </summary>
    public BackgroundJobsClusteringOptions Clustering { get; set; } = new();

    /// <summary>
    /// Overrides keyed by job type, for example
    /// <c>Endatix:BackgroundJobs:JobTypes:SubmissionExport:MaxAttempts</c>.
    /// </summary>
    /// <remarks>
    /// A key matches its job type regardless of case, even though job types themselves are case-sensitive:
    /// configuration keys are case-insensitive everywhere else, so an override written in a different case has to
    /// apply rather than be silently ignored. Only the values of <see cref="BackgroundJobTypeOptions"/> override;
    /// anything else written under a job type binds to nothing and is ignored.
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
            BackoffCap: TimeSpan.FromSeconds(overrides?.BackoffCapSeconds ?? BackoffCapSeconds),
            MaxConcurrency: overrides?.MaxConcurrency ?? DefaultJobTypeMaxConcurrency,
            Retention: TimeSpan.FromDays(overrides?.RetentionDays ?? RetentionDays));
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

    /// <summary>
    /// How many jobs of this type one node runs at once. <c>0</c> stops this node from running the type at all.
    /// Unset means <see cref="BackgroundJobsOptions.DefaultJobTypeMaxConcurrency"/>; there is no global value.
    /// </summary>
    public int? MaxConcurrency { get; set; }

    public int? RetentionDays { get; set; }
}

/// <summary>
/// Bound from <c>Endatix:BackgroundJobs:Clustering</c>.
/// </summary>
/// <remarks>
/// A node silent for <see cref="CheckinIntervalSeconds"/> plus <see cref="CheckinMisfireThresholdSeconds"/> is
/// presumed dead, and the jobs it was running are run again on another node. Every node's clock must agree with
/// the others' to within about a second, or a healthy node can be presumed dead.
/// </remarks>
public sealed class BackgroundJobsClusteringOptions
{
    public double CheckinIntervalSeconds { get; set; } = 7.5;

    public double CheckinMisfireThresholdSeconds { get; set; } = 7.5;

    /// <summary>
    /// This node's identity among the nodes sharing the store. Unset generates one from the host name and the
    /// start time, so a restarted node is a new node and a peer recovers what the old one left running. Set it
    /// only to a value no other running node uses.
    /// </summary>
    public string? InstanceId { get; set; }
}

/// <summary>
/// The values in effect for one job type, once its overrides have fallen back to the global values.
/// </summary>
internal readonly record struct BackgroundJobTypePolicy(
    int MaxAttempts,
    TimeSpan MaxRuntime,
    TimeSpan BackoffBase,
    TimeSpan BackoffCap,
    int MaxConcurrency,
    TimeSpan Retention);
