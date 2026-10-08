using Ardalis.GuardClauses;
using Endatix.Infrastructure.Features.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <remarks>
/// A job type's settings resolve one at a time, from <see cref="JobTypes"/>, the global values the host set and the
/// defaults the job type declares in code, so the runtime must read them through <see cref="ResolvePolicy"/>; reading
/// a global property gives only the global value the host set.
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Endatix:BackgroundJobs";

    /// <summary>
    /// The concurrency cap of a job type that neither the host nor the job type's own defaults set.
    /// </summary>
    public const int DefaultJobTypeMaxConcurrency = 1;

    /// <summary>The global <see cref="MaxRuntimeMinutes"/> where the host sets none.</summary>
    public const int DefaultMaxRuntimeMinutes = 60;

    /// <summary>The global <see cref="MaxAttempts"/> where the host sets none.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>The global <see cref="BackoffBaseSeconds"/> where the host sets none.</summary>
    public const int DefaultBackoffBaseSeconds = 30;

    /// <summary>The global <see cref="BackoffCapSeconds"/> where the host sets none.</summary>
    public const int DefaultBackoffCapSeconds = 900;

    /// <summary>The global <see cref="RetentionDays"/> where the host sets none.</summary>
    public const int DefaultRetentionDays = 7;

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
    /// How long a due job may wait for a free slot before it counts as misfired, which logs the backlog warning.
    /// </summary>
    public int MisfireThresholdSeconds { get; set; } = 60;

    /// <summary>
    /// How long a stopping host waits for running jobs to finish. A job still running when the wait ends is left
    /// as it is, with nothing recorded, and runs again on the next node to check in. The host's own shutdown timeout
    /// (<c>HostOptions.ShutdownTimeout</c>, 30 seconds by default) also bounds the wait, so a longer value needs a
    /// longer host timeout to take effect.
    /// </summary>
    public int ShutdownWaitSeconds { get; set; } = 30;

    // The five global values a job type can also set are null until the host sets them, because one the host sets
    // replaces the default a job type declares in code, and the global default does not. Unset, each is its Default
    // constant above.

    /// <summary>
    /// Ceiling on one attempt, in minutes.
    /// </summary>
    public int? MaxRuntimeMinutes { get; set; }

    /// <summary>
    /// Attempts before a job that keeps throwing is dead-lettered. The node running an attempt reads it when the
    /// attempt ends, so a change also applies to jobs already waiting.
    /// </summary>
    public int? MaxAttempts { get; set; }

    /// <summary>
    /// Seconds before the first retry. Each later retry waits twice as long as the one before it, up to
    /// <see cref="BackoffCapSeconds"/>. The node running an attempt reads it when the attempt fails.
    /// </summary>
    public int? BackoffBaseSeconds { get; set; }

    public int? BackoffCapSeconds { get; set; }

    /// <summary>
    /// Days a finished job's row is kept before the retention job may delete it.
    /// </summary>
    public int? RetentionDays { get; set; }

    /// <summary>
    /// How finished job rows are collected once their retention has passed.
    /// </summary>
    public BackgroundJobsRetentionOptions Retention { get; set; } = new();

    /// <summary>
    /// The scheduler's operator dashboard. Off unless enabled.
    /// </summary>
    public BackgroundJobsDashboardOptions Dashboard { get; set; } = new();

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
    /// The policy <paramref name="jobType"/> runs with, each value resolved by <see cref="JobTypeSettings.Resolve"/>.
    /// </summary>
    internal BackgroundJobTypePolicy ResolvePolicy(string jobType, BackgroundJobTypeDefaults? declared) =>
        JobTypeSettings.Resolve(this, jobType, declared).ToPolicy();

    /// <summary>The configuration key of the global setting <paramref name="optionName"/>.</summary>
    internal static string GlobalKey(string optionName) => $"{SectionName}:{optionName}";

    /// <summary>The configuration key of <paramref name="jobType"/>'s own setting <paramref name="optionName"/>.</summary>
    internal static string JobTypeKey(string jobType, string optionName) =>
        $"{SectionName}:{nameof(JobTypes)}:{jobType}:{optionName}";
}

/// <summary>
/// Overrides for one job type, bound from <c>Endatix:BackgroundJobs:JobTypes:{JobType}</c>. A value left unset
/// falls back to the global value of the same name on <see cref="BackgroundJobsOptions"/> when the host set it, then
/// to the default the job type declares in code, then to the global default.
/// </summary>
public sealed class BackgroundJobTypeOptions
{
    public int? MaxAttempts { get; set; }

    public int? MaxRuntimeMinutes { get; set; }

    public int? BackoffBaseSeconds { get; set; }

    public int? BackoffCapSeconds { get; set; }

    /// <summary>
    /// How many jobs of this type one node runs at once. <c>0</c> stops this node from running the type at all.
    /// Unset means the job type's declared default, else <see cref="BackgroundJobsOptions.DefaultJobTypeMaxConcurrency"/>;
    /// there is no global value.
    /// </summary>
    public int? MaxConcurrency { get; set; }

    public int? RetentionDays { get; set; }
}

/// <summary>
/// Bound from <c>Endatix:BackgroundJobs:Retention</c>.
/// </summary>
public sealed class BackgroundJobsRetentionOptions
{
    /// <summary>When the retention job runs, as a Quartz cron expression. Every 15 minutes by default.</summary>
    public string Cron { get; set; } = "0 0/15 * * * ?";

    /// <summary>Rows deleted per statement, so one run never holds a long lock.</summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>Batches one run deletes at most; the rest wait for the next run.</summary>
    public int MaxBatchesPerRun { get; set; } = 50;
}

/// <summary>
/// Bound from <c>Endatix:BackgroundJobs:Dashboard</c>.
/// </summary>
/// <remarks>
/// The dashboard and its HTTP API are for platform operators only: an authorized caller that may write can
/// schedule any job, which is code execution on the host, and every tenant's triggers are visible there.
/// </remarks>
public sealed class BackgroundJobsDashboardOptions
{
    /// <summary>Maps the dashboard at <c>/quartz</c> and its HTTP API at <c>/quartz-api</c>, for platform admins.</summary>
    public bool Enabled { get; set; }

    /// <summary>Allows changes through the dashboard and its HTTP API. Read-only otherwise.</summary>
    public bool AllowWrites { get; set; }
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
/// The values in effect for one job type, as <see cref="JobTypeSettings.Resolve"/> resolves them.
/// </summary>
internal readonly record struct BackgroundJobTypePolicy(
    int MaxAttempts,
    TimeSpan MaxRuntime,
    TimeSpan BackoffBase,
    TimeSpan BackoffCap,
    int MaxConcurrency,
    TimeSpan Retention);
