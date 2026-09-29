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
    /// The one-off trigger of job row <paramref name="jobId"/>. Its key is the job id, its only data is the job
    /// id, and it carries the job type's execution group and retry policy. The payload and anything secret stay
    /// on the row. With <paramref name="reclaim"/>, it also marks the firing as taking the job over from an attempt
    /// whose outcome could not be written.
    /// </summary>
    public static ITrigger TriggerFor(
        long jobId,
        string jobType,
        BackgroundJobTypePolicy policy,
        DateTimeOffset? startAt = null,
        bool reclaim = false)
    {
        var id = jobId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(id, jobType)
            .ForJob(JobKeyFor(jobType))
            .WithExecutionGroup(jobType)
            .UsingJobData(BackgroundJobExecution.JobIdKey, id);
        trigger = startAt is { } at ? trigger.StartAt(at) : trigger.StartNow();
        if (reclaim)
        {
            trigger = trigger.UsingJobData(BackgroundJobExecution.ReclaimKey, bool.TrueString);
        }

        var retryPolicy = BackgroundJobRetryPolicy.ToQuartz(policy);
        if (retryPolicy is not null)
        {
            trigger = trigger.WithRetryPolicy(retryPolicy);
        }

        return trigger.Build();
    }

    public static IServiceCollection AddJobsScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();
        var connectionString = ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration);

        services.AddScoped<BackgroundJobExecution>();
        services.AddSingleton<JobsShutdownSignal>();
        services.AddSingleton<IJobExecutionContextResolver, JobRowExecutionContextResolver>();
        services.AddMetrics();
        services.TryAddSingleton<IJobMetrics, MeterJobMetrics>();
        services.AddScoped<IJobTriggerScheduler, QuartzJobTriggerScheduler>();
        services.AddScoped<IBackgroundJobStateRepository, BackgroundJobStateRepository>();

        services.AddQuartz(SchedulerName, quartz =>
        {
            quartz.ConfigureScheduler(scheduler =>
            {
                scheduler.InstanceName = SchedulerName;
                if (string.IsNullOrWhiteSpace(options.Clustering.InstanceId))
                {
                    scheduler.GenerateInstanceId = true;
                }
                else
                {
                    scheduler.InstanceId = options.Clustering.InstanceId;
                }

                scheduler.IdleWaitTime = TimeSpan.FromSeconds(options.IdleWaitTimeSeconds);

                // The wrapper re-parents each run onto the trace stored on the job row, so the trigger needs no
                // trace data of its own and carries nothing but the job id.
                scheduler.PropagateTraceContext = false;

                // A handler cancelled by shutdown would be recorded as finished and never run again, so shutdown
                // never cancels a running job through the scheduler.
                scheduler.ShutdownJobInterruption = ShutdownJobInterruption.Never;
            });

            quartz.AddTriggerListener<JobTriggerListener>();

            if (options.RunInProcess)
            {
                quartz.UseDefaultThreadPool();
            }
            else
            {
                quartz.UseThreadPool<ZeroSizeThreadPool>();
            }

            quartz.UsePersistentStore(store =>
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
                    clustering.CheckinMisfireThreshold =
                        TimeSpan.FromSeconds(options.Clustering.CheckinMisfireThresholdSeconds);
                });
            });
        });

        // Sized from the registry, which only exists once the container is built.
        services.AddOptions<ThreadPoolOptions>(SchedulerName)
            .Configure<JobHandlerRegistry, IOptions<BackgroundJobsOptions>>((threadPool, registry, jobsOptions) =>
                threadPool.MaxConcurrency = Build(registry.JobTypes, jobsOptions.Value).PoolSize);

        return services;
    }
}

/// <param name="PoolSize">How many jobs this node runs at once, across every job type.</param>
internal sealed record JobsSchedulerPlan(int PoolSize);
