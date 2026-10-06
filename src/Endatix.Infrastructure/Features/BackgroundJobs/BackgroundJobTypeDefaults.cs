namespace Endatix.Infrastructure.Features.BackgroundJobs;

/// <summary>
/// The settings a job type runs with on a host that configures none of its own, declared by the job type's owner
/// when it registers the handler, so every host runs the job type as tuned, not only one whose configuration repeats
/// them. A value left unset falls back to the host's global value of the same name.
/// </summary>
/// <remarks>
/// A host still overrides each of them under <c>Endatix:BackgroundJobs:JobTypes:{JobType}</c>. They take precedence
/// over the global values under <c>Endatix:BackgroundJobs</c>, configured or not: a global value is the setting of
/// the job types that declare none.
/// </remarks>
public sealed record BackgroundJobTypeDefaults
{
    /// <summary>The job type these defaults apply to, set when they are registered for it.</summary>
    public string JobType { get; internal init; } = string.Empty;

    public int? MaxAttempts { get; init; }

    public int? MaxRuntimeMinutes { get; init; }

    public int? BackoffBaseSeconds { get; init; }

    public int? BackoffCapSeconds { get; init; }

    public int? RetentionDays { get; init; }

    /// <summary>How many jobs of this type one node runs at once.</summary>
    public int? MaxConcurrency { get; init; }
}
