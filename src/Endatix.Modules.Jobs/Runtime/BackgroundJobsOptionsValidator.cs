using Endatix.Infrastructure.Features.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <remarks>
/// <para>
/// Unknown job types, unknown per-type keys and unknown sibling sections pass. Overrides for a job type whose
/// handler is not deployed yet, or a section another component reads, are not mistakes.
/// </para>
/// <para>
/// A job type is validated on the values it runs with, as <see cref="JobTypeSettings.Resolve"/> resolves them, each
/// named by the key it came from. A value the job type takes from the global one is validated once, as the global
/// value, rather than once per job type.
/// </para>
/// </remarks>
internal sealed class BackgroundJobsOptionsValidator(JobTypeDefaults declaredDefaults) : IValidateOptions<BackgroundJobsOptions>
{
    // Every value is bounded above as well as below. A value far above these does not tune the queue: it breaks
    // the timers and date arithmetic that run jobs, or asks one process for more work than it can carry, and the
    // host is better off failing to start than running with it. A job type's override is bounded like the global
    // value it replaces.
    private const int MaxAttemptsCeiling = 10;
    private const int MaxConcurrencyCeiling = 1000;
    private const int IdleWaitCeilingSeconds = 300;
    private const double CheckinCeilingSeconds = 300;
    private const int OneHourInSeconds = 3600;
    private const int TenYearsInDays = 3650;
    private const int MaxRetentionBatchSize = 100_000;
    private const int MaxRetentionBatches = 10_000;
    private const int OneDayInMinutes = 1440;
    private const int OneDayInSeconds = 86_400;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        List<string> failures = [];

        ValidateHostSettings(options, failures);
        ValidateRetention(options, failures);
        ValidateGlobalJobTypeValues(options, failures);
        foreach (var jobType in JobTypesToValidate(options))
        {
            ValidateJobType(JobTypeSettings.Resolve(options, jobType, declaredDefaults.For(jobType)), failures);
        }

        return failures.Count is 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateHostSettings(BackgroundJobsOptions options, List<string> failures)
    {
        RequireInRange(options.IdleWaitTimeSeconds, GlobalKey(nameof(options.IdleWaitTimeSeconds)), IdleWaitCeilingSeconds, failures);
        RequireInRange(options.CancellationPollSeconds, GlobalKey(nameof(options.CancellationPollSeconds)), OneHourInSeconds, failures);
        RequireInRange(options.RetentionDays, GlobalKey(nameof(options.RetentionDays)), TenYearsInDays, failures);
        RequireInRange(options.MisfireThresholdSeconds, GlobalKey(nameof(options.MisfireThresholdSeconds)), OneHourInSeconds, failures);
        RequireInRange(options.ShutdownWaitSeconds, GlobalKey(nameof(options.ShutdownWaitSeconds)), OneHourInSeconds, failures);
        RequirePositiveSeconds(
            options.Clustering?.CheckinIntervalSeconds,
            ClusteringKey(nameof(BackgroundJobsClusteringOptions.CheckinIntervalSeconds)),
            failures);
        RequirePositiveSeconds(
            options.Clustering?.CheckinMisfireThresholdSeconds,
            ClusteringKey(nameof(BackgroundJobsClusteringOptions.CheckinMisfireThresholdSeconds)),
            failures);
    }

    private static void ValidateRetention(BackgroundJobsOptions options, List<string> failures)
    {
        RequireInRange(options.Retention?.BatchSize, RetentionKey(nameof(BackgroundJobsRetentionOptions.BatchSize)), MaxRetentionBatchSize, failures);
        RequireInRange(options.Retention?.MaxBatchesPerRun, RetentionKey(nameof(BackgroundJobsRetentionOptions.MaxBatchesPerRun)), MaxRetentionBatches, failures);
        if (options.Retention?.Cron is not { } cron || !Quartz.CronExpression.TryParse(cron, out _))
        {
            failures.Add($"{RetentionKey(nameof(BackgroundJobsRetentionOptions.Cron))} must be a valid Quartz cron expression, but is '{options.Retention?.Cron}'.");
        }
    }

    // The global values every job type falls back to.
    private static void ValidateGlobalJobTypeValues(BackgroundJobsOptions options, List<string> failures)
    {
        RequireInRange(options.MaxRuntimeMinutes, GlobalKey(nameof(options.MaxRuntimeMinutes)), OneDayInMinutes, failures);
        RequireInRange(options.MaxAttempts, GlobalKey(nameof(options.MaxAttempts)), MaxAttemptsCeiling, failures);
        RequireInRange(options.BackoffBaseSeconds, GlobalKey(nameof(options.BackoffBaseSeconds)), OneDayInSeconds, failures);
        RequireInRange(options.BackoffCapSeconds, GlobalKey(nameof(options.BackoffCapSeconds)), OneDayInSeconds, failures);
        RequireCapNotBelowBase(
            Global(options.BackoffBaseSeconds, BackgroundJobsOptions.DefaultBackoffBaseSeconds, nameof(options.BackoffBaseSeconds)),
            Global(options.BackoffCapSeconds, BackgroundJobsOptions.DefaultBackoffCapSeconds, nameof(options.BackoffCapSeconds)),
            failures);
    }

    // A configured key that differs from a declared job type only in case is that job type, as it is when the
    // policy is resolved, so it is validated once, under the declared name.
    private IEnumerable<string> JobTypesToValidate(BackgroundJobsOptions options) =>
        declaredDefaults.JobTypes
            .Union(options.JobTypes.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);

    private static void ValidateJobType(JobTypeSettings settings, List<string> failures)
    {
        (JobTypeSetting Setting, int Maximum, int Minimum)[] bounded =
        [
            (settings.MaxAttempts, MaxAttemptsCeiling, 1),
            (settings.MaxRuntimeMinutes, OneDayInMinutes, 1),
            (settings.BackoffBaseSeconds, OneDayInSeconds, 1),
            (settings.BackoffCapSeconds, OneDayInSeconds, 1),
            (settings.RetentionDays, TenYearsInDays, 1),

            // Zero is a real setting: it keeps this node from running the type while it still enqueues it.
            (settings.MaxConcurrency, MaxConcurrencyCeiling, 0),
        ];
        foreach (var (setting, maximum, minimum) in bounded.Where(bound => !bound.Setting.IsGlobal))
        {
            RequireInRange(setting.Value, setting.Key, maximum, failures, minimum);
        }

        RequireBackoffCapNotBelowBase(settings, failures);
    }

    // With both values the global ones, the pair has already been checked. Otherwise each side is named by the key
    // its value came from, so a clash with an inherited global value points the operator at that global key.
    private static void RequireBackoffCapNotBelowBase(JobTypeSettings settings, List<string> failures)
    {
        if (!settings.BackoffBaseSeconds.IsGlobal || !settings.BackoffCapSeconds.IsGlobal)
        {
            RequireCapNotBelowBase(settings.BackoffBaseSeconds, settings.BackoffCapSeconds, failures);
        }
    }

    private static void RequireInRange(int? value, string key, int maximum, List<string> failures, int minimum = 1)
    {
        if (value < minimum)
        {
            failures.Add($"{key} must be at least {minimum}, but is {value}.");
        }
        else if (value > maximum)
        {
            failures.Add($"{key} must be at most {maximum}, but is {value}.");
        }
    }

    private static void RequirePositiveSeconds(double? value, string key, List<string> failures)
    {
        if (value is null)
        {
            failures.Add($"{key} is required.");
        }
        else if (!(value > 0) || value > CheckinCeilingSeconds)
        {
            failures.Add($"{key} must be greater than 0 and at most {CheckinCeilingSeconds}, but is {value}.");
        }
    }

    private static void RequireCapNotBelowBase(JobTypeSetting backoffBase, JobTypeSetting backoffCap, List<string> failures)
    {
        // A value below 1 has already been reported, and comparing it would only add a confusing second message.
        if (backoffBase.Value is not >= 1 || backoffCap.Value is not >= 1)
        {
            return;
        }

        if (backoffCap.Value < backoffBase.Value)
        {
            failures.Add(
                $"{backoffCap.Key} ({backoffCap.Value}) must be greater than or equal to {backoffBase.Key} ({backoffBase.Value}).");
        }
    }

    private static JobTypeSetting Global(int? configured, int globalDefault, string optionName) =>
        configured is { } value
            ? new(value, JobTypeSettingSource.GlobalConfiguration, GlobalKey(optionName))
            : new(globalDefault, JobTypeSettingSource.GlobalDefault, GlobalKey(optionName));

    private static string GlobalKey(string optionName) => BackgroundJobsOptions.GlobalKey(optionName);

    private static string RetentionKey(string optionName) =>
        GlobalKey($"{nameof(BackgroundJobsOptions.Retention)}:{optionName}");

    private static string ClusteringKey(string optionName) =>
        GlobalKey($"{nameof(BackgroundJobsOptions.Clustering)}:{optionName}");
}
