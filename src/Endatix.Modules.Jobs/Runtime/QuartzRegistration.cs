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

    /// <summary>
    /// What the scheduler is sized to from this host's job types. The pool holds every job type's full cap at
    /// once, so a backlog in one job type never takes a thread another type is entitled to.
    /// </summary>
    public static JobsSchedulerPlan Build(IEnumerable<string> jobTypes, BackgroundJobsOptions options)
    {
        var poolSize = jobTypes.Sum(jobType => options.ResolvePolicy(jobType).MaxConcurrency);

        // A pool of zero is not a valid thread pool; a host whose job types are all capped at zero keeps one
        // thread it never uses.
        return new JobsSchedulerPlan(Math.Max(1, poolSize));
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
        services.AddQuartz(SchedulerName, quartz => ConfigureQuartz(quartz, options, connectionString));

        // Sized from the registry, which only exists once the container is built.
        services.AddOptions<ThreadPoolOptions>(SchedulerName)
            .Configure<JobHandlerRegistry, IOptions<BackgroundJobsOptions>>((threadPool, registry, jobsOptions) =>
                threadPool.MaxConcurrency = Build(registry.JobTypes, jobsOptions.Value).PoolSize);

        return services;
    }

    private static void ConfigureQuartz(IQuartzBuilder quartz, BackgroundJobsOptions options, string connectionString)
    {
        quartz.ConfigureScheduler(scheduler => ConfigureScheduler(scheduler, options));
        quartz.AddTriggerListener<JobTriggerListener>();

        if (options.RunInProcess)
        {
            quartz.UseDefaultThreadPool();
        }
        else
        {
            quartz.UseThreadPool<ZeroSizeThreadPool>();
        }

        quartz.UsePersistentStore(store => ConfigureStore(store, options, connectionString));
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

    private static void ConfigureStore(IPersistentStoreBuilder store, BackgroundJobsOptions options, string connectionString)
    {
        store.UsePostgres(connectionString);
        store.UseSystemTextJsonSerializer();
        store.ConfigureStore(ado =>
        {
            ado.TablePrefix = TablePrefix;
            ado.StoreJobDataAsStrings = true;
            ado.SchemaProvisioning = SchemaProvisioning.Validate;
            ado.AcceptEnlistedTransactions = true;
        });
        store.UseClustering(clustering =>
        {
            clustering.CheckinInterval = TimeSpan.FromSeconds(options.Clustering.CheckinIntervalSeconds);
            clustering.CheckinMisfireThreshold = TimeSpan.FromSeconds(options.Clustering.CheckinMisfireThresholdSeconds);
        });
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
internal sealed record JobsSchedulerPlan(int PoolSize);
