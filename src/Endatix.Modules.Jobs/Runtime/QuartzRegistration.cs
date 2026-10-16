using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    // Triggers already due join a batch whatever the window; the window lets a trigger due a moment later, such as
    // one enqueued by a node whose clock runs slightly ahead, join it rather than wait for a lock round of its own.
    // A delayed trigger fires at most this early, which is nothing beside the seconds a retry backoff or a re-fire
    // waits.
    private static readonly TimeSpan BatchFireAheadWindow = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// What the scheduler is sized to from this host's job types. The pool holds every job type's full cap at
    /// once, so a backlog in one job type never takes a thread another type is entitled to.
    /// </summary>
    public static JobsSchedulerPlan Build(IEnumerable<string> jobTypes, JobTypePolicies policies)
    {
        var caps = jobTypes.ToDictionary(
            jobType => jobType,
            jobType => policies.For(jobType).MaxConcurrency,
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
    /// The one-off trigger <paramref name="spec"/> describes. Its key is the job id, its data is the job id and, on a
    /// re-fire, what the re-fire is for, and it carries the job type's execution group. The payload and anything secret
    /// stay on the row; only a carried failure's message, the handler's own text, is in the trigger's data as well, and
    /// so on the dashboard (see <see cref="UnsavedOutcome"/>).
    /// </summary>
    /// <remarks>
    /// It carries no retry policy: the job wrapper schedules each retry on a trigger of its own, timed from the job
    /// type's backoff as the node is configured when the attempt fails.
    /// </remarks>
    public static ITrigger TriggerFor(JobTriggerSpec spec)
    {
        var id = spec.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(id, spec.JobType)
            .ForJob(JobKeyFor(spec.JobType))
            .WithExecutionGroup(spec.JobType)
            .UsingJobData(TriggerData(spec));
        trigger = spec.StartAt is { } at ? trigger.StartAt(at) : trigger.StartNow();
        return trigger.Build();
    }

    /// <summary>
    /// The data of the trigger <paramref name="spec"/> describes: the job id, and on a re-fire what it is for, taking the
    /// row over or writing the outcome an earlier firing could not.
    /// </summary>
    internal static JobDataMap TriggerData(JobTriggerSpec spec)
    {
        var data = new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = spec.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (spec.Reclaim)
        {
            data[BackgroundJobExecution.ReclaimKey] = bool.TrueString;
        }

        if (spec.Unsaved is { } unsaved)
        {
            data[UnsavedOutcome.DataKey] = unsaved.Serialize();
        }

        return data;
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
        services.AddScoped<RefusedRefire>();
        services.AddSingleton<StoredDurableJobs>();
        // Added only if absent, so a test can make a refused trigger's next try come sooner.
        services.TryAddSingleton(TriggerStoreBackoff.Default);
        services.AddScoped<JobLifecycleMetrics>();
        services.AddSingleton<JobsShutdownSignal>();
        services.AddSingleton<ReadOnlyDatabaseWarning>();
        services.AddSingleton<IJobExecutionContextResolver, JobRowExecutionContextResolver>();
        services.AddMetrics();
        services.TryAddSingleton<IJobMetrics, MeterJobMetrics>();
        AddJobTypePolicies(services);
        return services;
    }

    private static void AddJobTypePolicies(IServiceCollection services)
    {
        services.AddSingleton<JobTypeDefaults>();
        services.AddSingleton<JobTypePolicies>();
    }

    public static IServiceCollection AddJobsScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();
        var database = JobStoreDatabase.From(configuration, options);
        var sizing = new JobsSchedulerSizing();

        services.AddSingleton(database);
        services.AddSingleton(sizing);
        services.AddSingleton<ReadCommittedSnapshotRequirement>();
        services.AddJobExecution();
        services.AddScoped<IJobTriggerScheduler, QuartzJobTriggerScheduler>();
        services.AddScoped<IBackgroundJobStateRepository, BackgroundJobStateRepository>();
        services.AddScoped<JobRetentionJob>();
        services.AddQuartz(SchedulerName, quartz => ConfigureQuartz(quartz, new SchedulerRecipe(options, database, sizing)));
        ReadCommittedSnapshotGatedSchedulerFactory.Register(services);

        return services;
    }

    // Sized from the registry, which only exists once the application's container is built. Configured in the
    // recipe rather than beside it, because a rebuilt scheduler replays only the recipe; the sizing keeps the count the
    // application's container worked out, which the rebuilt one's container cannot.
    private static void SizeToRegisteredJobTypes(IQuartzBuilder quartz, JobsSchedulerSizing sizing)
    {
        quartz.Services.AddOptions<ThreadPoolOptions>(quartz.SchedulerName)
            .Configure<IServiceProvider>((threadPool, services) => threadPool.MaxConcurrency = sizing.ThreadCount(services));

        // An acquisition takes up to one trigger per free thread and fires them together, taking the cluster-wide
        // trigger lock once to acquire and once to fire rather than once per trigger. Above a batch of one, Quartz
        // takes the lock for every acquisition, so an idle node takes it once per idle wait. Execution limits still
        // hold per job type: the driver delegate never returns more of a type's triggers than it has free slots.
        quartz.Services.AddOptions<QuartzSchedulerOptions>(quartz.SchedulerName)
            .Configure<IServiceProvider>((scheduler, services) => scheduler.MaxBatchSize = sizing.ThreadCount(services));
    }

    // What the scheduler's recipe is built from; the recipe is replayed whenever the scheduler is rebuilt.
    private sealed record SchedulerRecipe(BackgroundJobsOptions Options, JobStoreDatabase Database, JobsSchedulerSizing Sizing);

    private static void ConfigureQuartz(IQuartzBuilder quartz, SchedulerRecipe recipe)
    {
        var (options, database, sizing) = recipe;
        quartz.ConfigureScheduler(scheduler => ConfigureScheduler(scheduler, options));
        SizeToRegisteredJobTypes(quartz, sizing);
        AddRetentionJob(quartz, options.Retention);
        quartz.AddTriggerListener<JobTriggerListener>();
        quartz.AddTriggerListener<JobMisfireListener>();
        quartz.UseExecutionLimits(ConfigureExecutionLimits);
        UseThreadPool(quartz, options);
        quartz.UsePersistentStore(store => ConfigureStore(store, options, database));
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
        scheduler.BatchTriggerAcquisitionFireAheadTimeWindow = BatchFireAheadWindow;

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
            provider.GetRequiredService<JobTypePolicies>());
        foreach (var (group, cap) in plan.GroupCaps)
        {
            limits.ForGroup(group, cap);
        }

        limits.ForGroup(JobRetentionJob.Group, JobsSchedulerSizing.MaintenanceThreads);
        limits.ForOtherGroups(0);
    }

    // Retention runs on one node at a time, in a group of its own so no job type's backlog delays it.
    private static void AddRetentionJob(IQuartzBuilder quartz, BackgroundJobsRetentionOptions? retention)
    {
        quartz.AddJob<JobRetentionJob>(job => job.WithIdentity(JobRetentionJob.Group, MaintenanceJobGroup).StoreDurably());

        // Parsing an invalid expression here would throw before the options validator can say which key is wrong;
        // without the trigger, the validator fails startup with that message instead.
        if (retention?.Cron is not { } cron || !CronExpression.TryParse(cron, out _))
        {
            return;
        }

        quartz.AddTrigger(trigger => trigger
            .WithIdentity(JobRetentionJob.Group, MaintenanceJobGroup)
            .ForJob(JobRetentionJob.Group, MaintenanceJobGroup)
            .WithExecutionGroup(JobRetentionJob.Group)
            .WithCronSchedule(cron));
    }

    private static void ConfigureStore(IPersistentStoreBuilder store, BackgroundJobsOptions options, JobStoreDatabase database)
    {
        UseDatabase(store, database);
        store.UseSystemTextJsonSerializer();
        store.ConfigureStore(ado => ConfigureAdoStore(ado, options, database));
        store.UseClustering(clustering =>
        {
            clustering.CheckinInterval = TimeSpan.FromSeconds(options.Clustering.CheckinIntervalSeconds);
            clustering.CheckinMisfireThreshold =
                TimeSpan.FromSeconds(options.Clustering.CheckinMisfireThresholdSeconds);
        });
    }

    // Each dialect is named before its database, because registration is first-wins and the database would otherwise
    // bring the shipped dialect.
    private static void UseDatabase(IPersistentStoreBuilder store, JobStoreDatabase database)
    {
        if (database.IsPostgreSql)
        {
            store.UseDriverDelegate<ExecutionGroupFilteringPostgreSqlDelegate>();
            store.UsePostgres(database.ConnectionString);
            return;
        }

        store.UseDriverDelegate<ExecutionGroupFilteringSqlServerDelegate>();
        store.UseSqlServer(database.ConnectionString);
    }

    private static void ConfigureAdoStore(AdoJobStoreOptions ado, BackgroundJobsOptions options, JobStoreDatabase database)
    {
        ado.TablePrefix = TablePrefix;
        ado.StoreJobDataAsStrings = true;
        ado.SchemaProvisioning = SchemaProvisioning.Validate;
        ado.AcceptEnlistedTransactions = true;

        // Off on PostgreSQL and on on SQL Server unless configured; JobStoreDatabase.From says why they differ, and the
        // snapshot check reads the same value.
        ado.LockOnInsert = database.LocksOnInsert;
        ado.MisfireThreshold = TimeSpan.FromSeconds(options.MisfireThresholdSeconds);

        // A job that waited past the threshold for a slot is put back in line by the misfire pass, so a pass as rare
        // as the threshold itself could hold it up to a whole threshold longer.
        ado.MisfireHandlerFrequency = TimeSpan.FromSeconds(Math.Min(10, options.MisfireThresholdSeconds));
    }
}

/// <param name="JobId">The job row the trigger fires.</param>
/// <param name="JobType">The job type whose durable scheduler job the trigger points at.</param>
/// <param name="StartAt">When the trigger first fires; now when <see langword="null"/>.</param>
/// <param name="Reclaim">
/// Whether the firing takes the job over from an attempt whose outcome could not be written.
/// </param>
/// <param name="Unsaved">
/// The outcome of that attempt, when its handler decided one, for the firing to write. It is stored in the trigger's
/// data, failure message included, where the dashboard shows it.
/// </param>
internal sealed record JobTriggerSpec(
    long JobId,
    string JobType,
    DateTimeOffset? StartAt = null,
    bool Reclaim = false,
    UnsavedOutcome? Unsaved = null);

/// <param name="PoolSize">How many jobs this node runs at once, across every job type.</param>
/// <param name="GroupCaps">
/// Each registered job type's execution group and how many of its jobs this node runs at once. Every other group
/// is capped at zero, so this node never acquires a job it has no handler for.
/// </param>
internal sealed record JobsSchedulerPlan(int PoolSize, IReadOnlyDictionary<string, int> GroupCaps);
