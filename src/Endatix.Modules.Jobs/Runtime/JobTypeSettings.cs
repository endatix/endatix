using Endatix.Infrastructure.Features.BackgroundJobs;
using static Endatix.Modules.Jobs.Runtime.BackgroundJobsOptions;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>Where a job type's setting came from, in the order they are tried.</summary>
internal enum JobTypeSettingSource
{
    /// <summary>The job type's own key, <c>Endatix:BackgroundJobs:JobTypes:{JobType}:{Setting}</c>.</summary>
    JobTypeConfiguration,

    /// <summary>The global key, <c>Endatix:BackgroundJobs:{Setting}</c>, which the host set.</summary>
    GlobalConfiguration,

    /// <summary>The default the job type declared in code when its handler was registered.</summary>
    JobTypeDefault,

    /// <summary>The default of the global value, which the host left unset.</summary>
    GlobalDefault,
}

/// <summary>One setting's value for a job type, where it came from, and the key an operator sets to change it.</summary>
internal readonly record struct JobTypeSetting(int Value, JobTypeSettingSource Source, string Key)
{
    /// <summary>Whether the value is the global one, which every job type that sets none of its own shares.</summary>
    public bool IsGlobal => Source is JobTypeSettingSource.GlobalConfiguration or JobTypeSettingSource.GlobalDefault;
}

/// <summary>
/// The values one job type runs with. Anything set in configuration wins over code, so each setting is taken from the
/// first of: the job type's own key, the global key when the host set it, the default the job type declared in code,
/// and the global default. <c>MaxConcurrency</c> has no global key, and its global default is
/// <see cref="BackgroundJobsOptions.DefaultJobTypeMaxConcurrency"/>.
/// </summary>
/// <remarks>
/// The runtime and the options validator both read a job type's settings here, so validation checks the values the
/// job type runs with.
/// </remarks>
internal sealed record JobTypeSettings(
    JobTypeSetting MaxAttempts,
    JobTypeSetting MaxRuntimeMinutes,
    JobTypeSetting BackoffBaseSeconds,
    JobTypeSetting BackoffCapSeconds,
    JobTypeSetting MaxConcurrency,
    JobTypeSetting RetentionDays)
{
    public static JobTypeSettings Resolve(BackgroundJobsOptions options, string jobType, BackgroundJobTypeDefaults? declared)
    {
        var configured = options.JobTypes.GetValueOrDefault(jobType) ?? new BackgroundJobTypeOptions();
        var defaults = declared ?? new BackgroundJobTypeDefaults();

        return new JobTypeSettings(
            MaxAttempts: Setting(jobType, nameof(MaxAttempts), new(
                configured.MaxAttempts, options.MaxAttempts, defaults.MaxAttempts, DefaultMaxAttempts)),
            MaxRuntimeMinutes: Setting(jobType, nameof(MaxRuntimeMinutes), new(
                configured.MaxRuntimeMinutes, options.MaxRuntimeMinutes, defaults.MaxRuntimeMinutes, DefaultMaxRuntimeMinutes)),
            BackoffBaseSeconds: Setting(jobType, nameof(BackoffBaseSeconds), new(
                configured.BackoffBaseSeconds, options.BackoffBaseSeconds, defaults.BackoffBaseSeconds, DefaultBackoffBaseSeconds)),
            BackoffCapSeconds: Setting(jobType, nameof(BackoffCapSeconds), new(
                configured.BackoffCapSeconds, options.BackoffCapSeconds, defaults.BackoffCapSeconds, DefaultBackoffCapSeconds)),
            MaxConcurrency: Setting(jobType, nameof(MaxConcurrency), SettingCandidates.WithoutGlobalKey(
                configured.MaxConcurrency, defaults.MaxConcurrency, DefaultJobTypeMaxConcurrency)),
            RetentionDays: Setting(jobType, nameof(RetentionDays), new(
                configured.RetentionDays, options.RetentionDays, defaults.RetentionDays, DefaultRetentionDays)));
    }

    public BackgroundJobTypePolicy ToPolicy() =>
        new(
            MaxAttempts: MaxAttempts.Value,
            MaxRuntime: TimeSpan.FromMinutes(MaxRuntimeMinutes.Value),
            BackoffBase: TimeSpan.FromSeconds(BackoffBaseSeconds.Value),
            BackoffCap: TimeSpan.FromSeconds(BackoffCapSeconds.Value),
            MaxConcurrency: MaxConcurrency.Value,
            Retention: TimeSpan.FromDays(RetentionDays.Value));

    // The first candidate that is set wins. A value from code is named by the key that replaces it, which is what an
    // operator can change.
    private static JobTypeSetting Setting(string jobType, string optionName, SettingCandidates candidates)
    {
        var jobTypeKey = JobTypeKey(jobType, optionName);
        var globalKey = GlobalKey(optionName);

        return candidates switch
        {
            { JobTypeConfigured: { } value } => new(value, JobTypeSettingSource.JobTypeConfiguration, jobTypeKey),
            { GlobalConfigured: { } value } => new(value, JobTypeSettingSource.GlobalConfiguration, globalKey),
            { JobTypeDefault: { } value } =>
                new(value, JobTypeSettingSource.JobTypeDefault, $"{jobTypeKey} (the job type's default, declared in code)"),
            { HasGlobalKey: true } => new(candidates.GlobalDefault, JobTypeSettingSource.GlobalDefault, globalKey),
            _ => new(candidates.GlobalDefault, JobTypeSettingSource.GlobalDefault, $"{jobTypeKey} (the default, declared in code)"),
        };
    }

    /// <summary>The values one setting can take, in the order they are tried.</summary>
    private readonly record struct SettingCandidates(
        int? JobTypeConfigured,
        int? GlobalConfigured,
        int? JobTypeDefault,
        int GlobalDefault)
    {
        public bool HasGlobalKey { get; private init; } = true;

        public static SettingCandidates WithoutGlobalKey(int? jobTypeConfigured, int? jobTypeDefault, int globalDefault) =>
            new(jobTypeConfigured, GlobalConfigured: null, jobTypeDefault, globalDefault) { HasGlobalKey = false };
    }
}
