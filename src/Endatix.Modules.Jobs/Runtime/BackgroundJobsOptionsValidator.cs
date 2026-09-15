using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Rejects <see cref="BackgroundJobsOptions"/> that jobs cannot run with, naming the full configuration key of
/// every invalid value so an operator can find it.
/// </summary>
/// <remarks>
/// Unknown job types, unknown per-type keys and unknown sibling sections pass. Overrides for a job type whose
/// handler is not deployed yet, or a section another component reads, are not mistakes.
/// </remarks>
internal sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    // A heartbeat can land a beat late under load. Leaving room for three intervals keeps a slow but healthy job
    // from being presumed lost.
    private const int MinimumHeartbeatsBeforeStuck = 3;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        List<string> failures = [];

        RequireAtLeastOne(options.MaxConcurrency, GlobalKey(nameof(options.MaxConcurrency)), failures);
        RequireAtLeastOne(options.SweepIntervalSeconds, GlobalKey(nameof(options.SweepIntervalSeconds)), failures);
        RequireAtLeastOne(options.SweepBatchSize, GlobalKey(nameof(options.SweepBatchSize)), failures);
        RequireAtLeastOne(options.StuckJobThresholdMinutes, GlobalKey(nameof(options.StuckJobThresholdMinutes)), failures);
        RequireAtLeastOne(options.HeartbeatIntervalSeconds, GlobalKey(nameof(options.HeartbeatIntervalSeconds)), failures);
        RequireAtLeastOne(options.MaxRuntimeMinutes, GlobalKey(nameof(options.MaxRuntimeMinutes)), failures);
        RequireAtLeastOne(options.MaxAttempts, GlobalKey(nameof(options.MaxAttempts)), failures);
        RequireAtLeastOne(options.BackoffBaseSeconds, GlobalKey(nameof(options.BackoffBaseSeconds)), failures);
        RequireAtLeastOne(options.BackoffCapSeconds, GlobalKey(nameof(options.BackoffCapSeconds)), failures);
        RequireAtLeastOne(options.BacklogWarningMinutes, GlobalKey(nameof(options.BacklogWarningMinutes)), failures);

        RequireStuckThresholdSpansHeartbeats(options, failures);

        RequireCapNotBelowBase(
            options.BackoffBaseSeconds,
            GlobalKey(nameof(options.BackoffBaseSeconds)),
            options.BackoffCapSeconds,
            GlobalKey(nameof(options.BackoffCapSeconds)),
            failures);

        foreach (var (jobType, overrides) in options.JobTypes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            ValidateJobType(options, jobType, overrides, failures);
        }

        return failures.Count is 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateJobType(
        BackgroundJobsOptions options,
        string jobType,
        BackgroundJobTypeOptions? overrides,
        List<string> failures)
    {
        if (overrides is null)
        {
            return;
        }

        RequireAtLeastOne(overrides.MaxAttempts, JobTypeKey(jobType, nameof(overrides.MaxAttempts)), failures);
        RequireAtLeastOne(overrides.MaxRuntimeMinutes, JobTypeKey(jobType, nameof(overrides.MaxRuntimeMinutes)), failures);
        RequireAtLeastOne(overrides.BackoffBaseSeconds, JobTypeKey(jobType, nameof(overrides.BackoffBaseSeconds)), failures);
        RequireAtLeastOne(overrides.BackoffCapSeconds, JobTypeKey(jobType, nameof(overrides.BackoffCapSeconds)), failures);

        // With neither value overridden the pair is the global one, which has already been checked.
        if (overrides.BackoffBaseSeconds is null && overrides.BackoffCapSeconds is null)
        {
            return;
        }

        // Each side is named by the key its value came from, so a clash with an inherited global value points
        // the operator at that global key.
        RequireCapNotBelowBase(
            overrides.BackoffBaseSeconds ?? options.BackoffBaseSeconds,
            overrides.BackoffBaseSeconds is null
                ? GlobalKey(nameof(options.BackoffBaseSeconds))
                : JobTypeKey(jobType, nameof(overrides.BackoffBaseSeconds)),
            overrides.BackoffCapSeconds ?? options.BackoffCapSeconds,
            overrides.BackoffCapSeconds is null
                ? GlobalKey(nameof(options.BackoffCapSeconds))
                : JobTypeKey(jobType, nameof(overrides.BackoffCapSeconds)),
            failures);
    }

    private static void RequireAtLeastOne(int? value, string key, List<string> failures)
    {
        if (value < 1)
        {
            failures.Add($"{key} must be at least 1, but is {value}.");
        }
    }

    private static void RequireStuckThresholdSpansHeartbeats(BackgroundJobsOptions options, List<string> failures)
    {
        // A value below 1 has already been reported, and comparing it would only add a confusing second message.
        if (options.StuckJobThresholdMinutes < 1 || options.HeartbeatIntervalSeconds < 1)
        {
            return;
        }

        // Widened to long because a large threshold in seconds overflows int.
        var thresholdSeconds = options.StuckJobThresholdMinutes * 60L;
        var requiredSeconds = MinimumHeartbeatsBeforeStuck * (long)options.HeartbeatIntervalSeconds;

        if (thresholdSeconds < requiredSeconds)
        {
            failures.Add(
                $"{GlobalKey(nameof(options.StuckJobThresholdMinutes))} ({options.StuckJobThresholdMinutes} minutes) " +
                $"must span at least {MinimumHeartbeatsBeforeStuck} times " +
                $"{GlobalKey(nameof(options.HeartbeatIntervalSeconds))} ({options.HeartbeatIntervalSeconds} seconds).");
        }
    }

    private static void RequireCapNotBelowBase(
        int baseSeconds,
        string baseKey,
        int capSeconds,
        string capKey,
        List<string> failures)
    {
        // A value below 1 has already been reported, and comparing it would only add a confusing second message.
        if (baseSeconds < 1 || capSeconds < 1)
        {
            return;
        }

        if (capSeconds < baseSeconds)
        {
            failures.Add($"{capKey} ({capSeconds}) must be greater than or equal to {baseKey} ({baseSeconds}).");
        }
    }

    private static string GlobalKey(string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{optionName}";

    private static string JobTypeKey(string jobType, string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.JobTypes)}:{jobType}:{optionName}";
}
