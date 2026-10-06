using Endatix.Infrastructure.Features.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <remarks>
/// <para>
/// Unknown job types, unknown per-type keys and unknown sibling sections pass. Overrides for a job type whose
/// handler is not deployed yet, or a section another component reads, are not mistakes.
/// </para>
/// <para>
/// A job type is validated on the values it runs with: its overrides, then the defaults it declares in code, then
/// the global values, each named by where it came from.
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
            ValidateJobType(options, Settings(options, jobType), failures);
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
            Global(options.BackoffBaseSeconds, nameof(options.BackoffBaseSeconds)),
            Global(options.BackoffCapSeconds, nameof(options.BackoffCapSeconds)),
            failures);
    }

    // A configured key that differs from a declared job type only in case is that job type, as it is when the
    // policy is resolved, so it is validated once, under the declared name.
    private IEnumerable<string> JobTypesToValidate(BackgroundJobsOptions options) =>
        declaredDefaults.JobTypes
            .Union(options.JobTypes.Keys, StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);

    private JobTypeSettings Settings(BackgroundJobsOptions options, string jobType) =>
        new(
            jobType,
            options.JobTypes.GetValueOrDefault(jobType) ?? new BackgroundJobTypeOptions(),
            declaredDefaults.For(jobType) ?? new BackgroundJobTypeDefaults());

    private static void ValidateJobType(BackgroundJobsOptions options, JobTypeSettings settings, List<string> failures)
    {
        settings.MaxAttempts.RequireWithinRange(MaxAttemptsCeiling, failures);
        settings.MaxRuntimeMinutes.RequireWithinRange(OneDayInMinutes, failures);
        settings.BackoffBaseSeconds.RequireWithinRange(OneDayInSeconds, failures);
        settings.BackoffCapSeconds.RequireWithinRange(OneDayInSeconds, failures);
        settings.RetentionDays.RequireWithinRange(TenYearsInDays, failures);

        // Zero is a real setting: it keeps this node from running the type while it still enqueues it.
        settings.MaxConcurrency.RequireWithinRange(MaxConcurrencyCeiling, failures, minimum: 0);

        RequireBackoffCapNotBelowBase(options, settings, failures);
    }

    // Each side is named by the key its value came from, so a clash with an inherited global value points the
    // operator at that global key.
    private static void RequireBackoffCapNotBelowBase(
        BackgroundJobsOptions options,
        JobTypeSettings settings,
        List<string> failures)
    {
        var (backoffBase, backoffCap) = (settings.BackoffBaseSeconds, settings.BackoffCapSeconds);

        // With neither value set for the job type the pair is the global one, which has already been checked.
        if (backoffBase.Value is null && backoffCap.Value is null)
        {
            return;
        }

        RequireCapNotBelowBase(
            backoffBase.Value is null ? Global(options.BackoffBaseSeconds, nameof(options.BackoffBaseSeconds)) : backoffBase,
            backoffCap.Value is null ? Global(options.BackoffCapSeconds, nameof(options.BackoffCapSeconds)) : backoffCap,
            failures);
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

    private static void RequireCapNotBelowBase(SettingValue backoffBase, SettingValue backoffCap, List<string> failures)
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

    private static SettingValue Global(int value, string optionName) => new(value, GlobalKey(optionName));

    private static string GlobalKey(string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{optionName}";

    private static string RetentionKey(string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.Retention)}:{optionName}";

    private static string ClusteringKey(string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.Clustering)}:{optionName}";

    private static string JobTypeKey(string jobType, string optionName) =>
        $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.JobTypes)}:{jobType}:{optionName}";

    /// <summary>One setting's value and the key an operator finds it under.</summary>
    /// <param name="Value">The value, or <see langword="null"/> when the global value applies.</param>
    private readonly record struct SettingValue(int? Value, string Key)
    {
        public void RequireWithinRange(int maximum, List<string> failures, int minimum = 1) =>
            BackgroundJobsOptionsValidator.RequireInRange(Value, Key, maximum, failures, minimum);
    }

    /// <summary>
    /// The value each setting of one job type takes before the global values apply: the host's override, else the
    /// default the job type declares in code.
    /// </summary>
    private sealed record JobTypeSettings(
        string JobType,
        BackgroundJobTypeOptions Configured,
        BackgroundJobTypeDefaults Declared)
    {
        public SettingValue MaxAttempts => InEffect(nameof(MaxAttempts), Configured.MaxAttempts, Declared.MaxAttempts);

        public SettingValue MaxRuntimeMinutes =>
            InEffect(nameof(MaxRuntimeMinutes), Configured.MaxRuntimeMinutes, Declared.MaxRuntimeMinutes);

        public SettingValue BackoffBaseSeconds =>
            InEffect(nameof(BackoffBaseSeconds), Configured.BackoffBaseSeconds, Declared.BackoffBaseSeconds);

        public SettingValue BackoffCapSeconds =>
            InEffect(nameof(BackoffCapSeconds), Configured.BackoffCapSeconds, Declared.BackoffCapSeconds);

        public SettingValue RetentionDays => InEffect(nameof(RetentionDays), Configured.RetentionDays, Declared.RetentionDays);

        public SettingValue MaxConcurrency =>
            InEffect(nameof(MaxConcurrency), Configured.MaxConcurrency, Declared.MaxConcurrency);

        // A declared value is named by the key that overrides it, which is what the operator can change.
        private SettingValue InEffect(string optionName, int? configured, int? declared) =>
            configured is not null
                ? new(configured, JobTypeKey(JobType, optionName))
                : new(declared, $"{JobTypeKey(JobType, optionName)} (the job type's default, declared in code)");
    }
}
