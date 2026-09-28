using Endatix.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Registers the scheduler that fires job triggers: a clustered store in the <c>jobs</c> schema, beside the job
/// rows it schedules.
/// </summary>
/// <remarks>
/// <para>
/// The scheduler's tables are created by the <c>jobs</c> migrations only. The scheduler validates them at
/// startup and never creates them, because deployments that run with automatic migrations off must not have a
/// second component creating tables.
/// </para>
/// <para>
/// A host that does not execute jobs still builds the scheduler, because enqueueing schedules triggers through
/// it, but on a thread pool of zero and never started, so it fires nothing.
/// </para>
/// </remarks>
internal static class QuartzRegistration
{
    /// <summary>Every node sharing the store must use this name.</summary>
    public const string SchedulerName = "endatix-jobs";

    /// <summary>The scheduler job group of every job type's durable job.</summary>
    public const string JobGroup = "endatix";

    public const string TablePrefix = "jobs.qrtz_";

    /// <summary>The scheduler job group of maintenance jobs such as retention, apart from job types.</summary>
    public const string MaintenanceJobGroup = "endatix-maintenance";

    // One thread beyond the job types' caps, for retention, so it never waits behind them and never takes their
    // slots.
    private const int MaintenanceThreads = 1;

    /// <summary>
    /// What the scheduler is sized to from this host's job types. The pool holds every job type's full cap at
    /// once, so a backlog in one job type never takes a thread another type is entitled to.
    /// </summary>
    public static JobsSchedulerPlan Build(IEnumerable<string> jobTypes, BackgroundJobsOptions options)
    {
        var caps = jobTypes.ToDictionary(
            jobType => jobType,
            jobType => options.ResolvePolicy(jobType).MaxConcurrency,
            StringComparer.Ordinal);

        // A pool of zero is not a valid thread pool; a host whose job types are all capped at zero keeps one
        // thread it never uses.
        return new JobsSchedulerPlan(Math.Max(1, caps.Values.Sum()), caps);
    }

    public static JobKey JobKeyFor(string jobType) => new(jobType, JobGroup);

    /// <summary>
    /// The durable scheduler job for <paramref name="jobType"/>. It asks for recovery, so a job cut off by a
    /// crashed or stopped node runs again on another.
    /// </summary>
    public static IJobDetail DurableJobFor(string jobType) =>
        JobBuilder.Create<BackgroundJobExecution>()
            .WithIdentity(JobKeyFor(jobType))
            .StoreDurably()
            .RequestRecovery()
            .Build();

    /// <summary>
    /// The one-off trigger <paramref name="spec"/> describes. Its key is the job id, its only data is the job id, and
    /// it carries the job type's execution group and retry policy. The payload and anything secret stay on the row.
    /// </summary>
    public static ITrigger TriggerFor(JobTriggerSpec spec)
    {
        var id = spec.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(id, spec.JobType)
            .ForJob(JobKeyFor(spec.JobType))
            .WithExecutionGroup(spec.JobType)
            .UsingJobData(BackgroundJobExecution.JobIdKey, id);
        trigger = spec.StartAt is { } at ? trigger.StartAt(at) : trigger.StartNow();
        if (spec.Reclaim)
        {
            trigger = trigger.UsingJobData(BackgroundJobExecution.ReclaimKey, bool.TrueString);
        }

        return WithRetryPolicy(trigger, spec.Policy).Build();
    }

    /// <summary>
    /// Registers the one scheduler job class and what it runs each attempt through. The scheduler resolves a job in
    /// a scope of its own, so the wrapper's parts are scoped; a host may supply its own metrics sink.
    /// </summary>
    public static IServiceCollection AddJobExecution(this IServiceCollection services)
    {
        services.AddScoped<BackgroundJobExecution>();
        services.AddScoped<JobFiringAdmission>();
        services.AddScoped<JobAttemptClaimer>();
        services.AddScoped<JobHandlerRunner>();
        services.AddScoped<JobOutcomeRecorder>();
        services.AddScoped<UnrecordedJobRefire>();
        services.AddScoped<JobLifecycleMetrics>();
        services.AddSingleton<JobsShutdownSignal>();
        services.AddSingleton<IJobExecutionContextResolver, JobRowExecutionContextResolver>();
        services.AddMetrics();
        services.TryAddSingleton<IJobMetrics, MeterJobMetrics>();
        return services;
    }

    public static IServiceCollection AddJobsScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        services.AddJobExecution();
        services.AddScoped<IJobTriggerScheduler, QuartzJobTriggerScheduler>();
        services.AddScoped<IBackgroundJobStateRepository, BackgroundJobStateRepository>();
        services.AddScoped<JobRetentionJob>();
        services.AddQuartz(SchedulerName, quartz => ConfigureQuartz(quartz, options, connectionString));

        // Sized from the registry, which only exists once the container is built.
        services.AddOptions<ThreadPoolOptions>(SchedulerName)
            .Configure<JobHandlerRegistry, IOptions<BackgroundJobsOptions>>((threadPool, registry, jobsOptions) =>
                threadPool.MaxConcurrency = Build(registry.JobTypes, jobsOptions.Value).PoolSize + MaintenanceThreads);

        return services;
    }

    private static void ConfigureQuartz(
        IQuartzBuilder quartz,
        BackgroundJobsOptions options,
        string connectionString)
    {
        quartz.ConfigureScheduler(scheduler => ConfigureScheduler(scheduler, options));
        AddRetentionJob(quartz, options.Retention);
        quartz.AddTriggerListener<JobTriggerListener>();
        quartz.AddTriggerListener<JobMisfireListener>();
        quartz.UseExecutionLimits(ConfigureExecutionLimits);
        UseThreadPool(quartz, options);
        quartz.UsePersistentStore(store => ConfigureStore(store, options, connectionString));
    }

    private static void UseThreadPool(IQuartzBuilder quartz, BackgroundJobsOptions options)
    {
        if (options.RunInProcess)
        {
            quartz.UseDefaultThreadPool();
            return;
        }

        quartz.UseThreadPool<ZeroSizeThreadPool>();
    }

    private static void ConfigureScheduler(QuartzSchedulerOptions scheduler, BackgroundJobsOptions options)
    {
        scheduler.InstanceName = SchedulerName;
        SetInstanceId(scheduler, options.Clustering.InstanceId);
        scheduler.IdleWaitTime = TimeSpan.FromSeconds(options.IdleWaitTimeSeconds);

        // The wrapper re-parents each run onto the trace stored on the job row, so the trigger needs no trace data
        // of its own and carries nothing but the job id.
        scheduler.PropagateTraceContext = false;

        // A handler cancelled by shutdown would be recorded as finished and never run again, so shutdown never
        // cancels a running job through the scheduler.
        scheduler.ShutdownJobInterruption = ShutdownJobInterruption.Never;
    }

    // A node configured without an id gets one generated at startup.
    private static void SetInstanceId(QuartzSchedulerOptions scheduler, string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            scheduler.GenerateInstanceId = true;
            return;
        }

        scheduler.InstanceId = instanceId;
    }

    // Each registered job type's group is capped at its own concurrency, and every other group at zero, so this node
    // never acquires a job it has no handler for.
    private static void ConfigureExecutionLimits(IServiceProvider provider, ExecutionLimitsBuilder limits)
    {
        var plan = Build(
            provider.GetRequiredService<JobHandlerRegistry>().JobTypes,
            provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value);
        foreach (var (group, cap) in plan.GroupCaps)
        {
            limits.ForGroup(group, cap);
        }

        limits.ForGroup(JobRetentionJob.Group, MaintenanceThreads);
        limits.ForOtherGroups(0);
    }

    // Retention runs on one node at a time, in a group of its own so no job type's backlog delays it.
    private static void AddRetentionJob(IQuartzBuilder quartz, BackgroundJobsRetentionOptions retention)
    {
        quartz.AddJob<JobRetentionJob>(job => job.WithIdentity(JobRetentionJob.Group, MaintenanceJobGroup).StoreDurably());
        quartz.AddTrigger(trigger => trigger
            .WithIdentity(JobRetentionJob.Group, MaintenanceJobGroup)
            .ForJob(JobRetentionJob.Group, MaintenanceJobGroup)
            .WithExecutionGroup(JobRetentionJob.Group)
            .WithCronSchedule(retention.Cron));
    }

    private static void ConfigureStore(IPersistentStoreBuilder store, BackgroundJobsOptions options, string connectionString)
    {
        // Named before the database, because registration is first-wins and the database would otherwise bring the
        // shipped dialect.
        store.UseDriverDelegate<ExecutionGroupFilteringPostgreSqlDelegate>();
        store.UsePostgres(connectionString);
        store.UseSystemTextJsonSerializer();
        store.ConfigureStore(ado => ConfigureAdoStore(ado, options));
        store.UseClustering(clustering =>
        {
            clustering.CheckinInterval = TimeSpan.FromSeconds(options.Clustering.CheckinIntervalSeconds);
            clustering.CheckinMisfireThreshold =
                TimeSpan.FromSeconds(options.Clustering.CheckinMisfireThresholdSeconds);
        });
    }

    private static void ConfigureAdoStore(AdoJobStoreOptions ado, BackgroundJobsOptions options)
    {
        ado.TablePrefix = TablePrefix;
        ado.StoreJobDataAsStrings = true;
        ado.SchemaProvisioning = SchemaProvisioning.Validate;
        ado.AcceptEnlistedTransactions = true;
        ado.MisfireThreshold = TimeSpan.FromSeconds(options.MisfireThresholdSeconds);

        // A job that waited past the threshold for a slot is put back in line by the misfire pass, so a pass as rare
        // as the threshold itself could hold it up to a whole threshold longer.
        ado.MisfireHandlerFrequency = TimeSpan.FromSeconds(Math.Min(10, options.MisfireThresholdSeconds));
    }

    private static TriggerBuilder<IJob> WithRetryPolicy(TriggerBuilder<IJob> trigger, BackgroundJobTypePolicy policy) =>
        BackgroundJobRetryPolicy.ToQuartz(policy) is { } retryPolicy ? trigger.WithRetryPolicy(retryPolicy) : trigger;
}

/// <param name="JobId">The job row the trigger fires.</param>
/// <param name="JobType">The job type whose durable scheduler job the trigger points at.</param>
/// <param name="Policy">The job type's policy, which the trigger's retry policy follows.</param>
/// <param name="StartAt">When the trigger first fires; now when <see langword="null"/>.</param>
/// <param name="Reclaim">
/// Whether the firing takes the job over from an attempt whose outcome could not be written.
/// </param>
internal sealed record JobTriggerSpec(
    long JobId,
    string JobType,
    BackgroundJobTypePolicy Policy,
    DateTimeOffset? StartAt = null,
    bool Reclaim = false);

/// <param name="PoolSize">How many jobs this node runs at once, across every job type.</param>
/// <param name="GroupCaps">
/// Each registered job type's execution group and how many of its jobs this node runs at once. Every other group
/// is capped at zero, so this node never acquires a job it has no handler for.
/// </param>
internal sealed record JobsSchedulerPlan(int PoolSize, IReadOnlyDictionary<string, int> GroupCaps);
