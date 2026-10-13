using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;
using Quartz.Impl.AdoJobStore.Common;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A migrated jobs database of its own, a scheduler that only stores triggers in it, and the module's driver delegate
/// for the database's provider, so a test can seed triggers and call the delegate's acquisition directly, with the
/// limits it chooses, as the scheduler thread would.
/// </summary>
internal sealed class AcquisitionTestStore : IAsyncDisposable
{
    /// <summary>The job types every store has a durable job for.</summary>
    public static readonly string[] JobTypes = ["A", "B"];

    private readonly ServiceProvider _services;
    private readonly IScheduler _scheduler;

    private AcquisitionTestStore(JobsTestDatabase database, ServiceProvider services, IScheduler scheduler)
    {
        Database = database;
        _services = services;
        _scheduler = scheduler;
    }

    public JobsTestDatabase Database { get; }

    /// <summary>
    /// The driver delegate the module registered for the configured provider, as the scheduler's job store
    /// initialized it, so acquisition in a test runs through what a node would use.
    /// </summary>
    public IDriverDelegate DriverDelegate =>
        _services.GetRequiredKeyedService<IDriverDelegate>(QuartzRegistration.SchedulerName);

    /// <summary>When every trigger a test seeds is due: well before now, and well after the misfire cut-off.</summary>
    public DateTimeOffset Due { get; } = DateTimeOffset.UtcNow.AddMinutes(-30);

    /// <summary>
    /// Creates a database of <paramref name="provider"/>, migrates it as the module does and builds a scheduler on
    /// it from the module's registration, which validates the scheduler's tables as a node's would.
    /// </summary>
    public static async Task<AcquisitionTestStore> CreateAsync(
        TestDatabaseProvider provider,
        string serverConnectionString,
        CancellationToken cancellationToken)
    {
        var database = await JobsTestDatabase.CreateAsync(provider, serverConnectionString, cancellationToken);
        try
        {
            var configuration = Configuration(provider, database.ConnectionString);
            await MigrateAsync(provider, configuration, cancellationToken);
            var services = SchedulerServices(configuration);
            var scheduler = await SchedulerOf(services, cancellationToken);
            foreach (var jobType in JobTypes)
            {
                await scheduler.AddJob(QuartzRegistration.DurableJobFor(jobType), AddJobOptions.Replacing, cancellationToken);
            }

            return new AcquisitionTestStore(database, services, scheduler);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>The configuration a node on <paramref name="provider"/> would have, for a store only.</summary>
    public static IConfiguration Configuration(TestDatabaseProvider provider, string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] =
                    provider == TestDatabaseProvider.PostgreSql ? "postgresql" : "sqlserver",
                ["Endatix:BackgroundJobs:RunInProcess"] = "false",
            })
            .Build();

    /// <summary>
    /// The module's scheduler registration alone, with handlers for <see cref="JobTypes"/>; the module itself still
    /// refuses SQL Server.
    /// </summary>
    public static ServiceProvider SchedulerServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton(JobHandlerRegistry.Build(JobTypes.Select(jobType => new NoopHandler(jobType))));
        services.AddJobsScheduler(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>Builds the scheduler, which is when it validates its tables.</summary>
    public static ValueTask<IScheduler> SchedulerOf(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);

    /// <summary>
    /// Stores <paramref name="trigger"/> for its job type's durable job, due when it says after <see cref="Due"/>.
    /// </summary>
    public async Task ScheduleAsync(AcquisitionTestTrigger trigger, CancellationToken cancellationToken)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(trigger.Key)
            .ForJob(QuartzRegistration.JobKeyFor(trigger.JobType))
            .WithPriority(trigger.Priority)
            .StartAt(Due.AddSeconds(trigger.SecondsAfterDue));
        if (trigger.ExecutionGroup is { } group)
        {
            builder = builder.WithExecutionGroup(group);
        }

        await _scheduler.ScheduleJob(builder.Build(), cancellationToken: cancellationToken);
    }

    /// <summary>Marks <paramref name="trigger"/> acquired, as a node does before firing it, so it is no longer waiting.</summary>
    public Task MarkAcquiredAsync(TriggerKey trigger, CancellationToken cancellationToken) =>
        WithDelegateAsync(
            (driverDelegate, holder) => driverDelegate
                .UpdateTriggerState(holder, trigger, StoredTriggerState.Acquired, cancellationToken).AsTask(),
            cancellationToken);

    /// <summary>
    /// Seeds a backlog at the head of the queue: group <c>A</c>, capped at one, has one trigger firing and five more
    /// waiting, all due before group <c>B</c>'s only waiting trigger, whose key it returns.
    /// </summary>
    public async Task<TriggerKey> SeedHeadOfLineAsync(CancellationToken cancellationToken)
    {
        var firing = AcquisitionTestTrigger.Of("A", "a0", 0);
        await ScheduleAsync(firing, cancellationToken);
        await MarkAcquiredAsync(firing.Key, cancellationToken);
        for (var i = 1; i <= 5; i++)
        {
            await ScheduleAsync(AcquisitionTestTrigger.Of("A", $"a{i}", i), cancellationToken);
        }

        var behind = AcquisitionTestTrigger.Of("B", "b1", 10);
        await ScheduleAsync(behind, cancellationToken);
        return behind.Key;
    }

    /// <summary>
    /// Runs the module's driver delegate's acquisition with <paramref name="maxCount"/> and the free slots
    /// <paramref name="freeSlots"/> sets; every other group has none.
    /// </summary>
    public async Task<List<TriggerKey>> SelectTriggersToAcquireAsync(
        int maxCount,
        Action<ExecutionLimitsBuilder> freeSlots,
        CancellationToken cancellationToken)
    {
        var limits = ExecutionLimitsBuilder.Create();
        freeSlots(limits);
        var now = DateTimeOffset.UtcNow;
        var criteria = new TriggerAcquisitionCriteria
        {
            NoLaterThan = now.AddSeconds(30),
            NoEarlierThan = now.AddDays(-1),
            LiveNodeCutoff = now.AddSeconds(-15),
            MaxCount = maxCount,
            ExecutionLimits = limits.ForOtherGroups(0).Build(),
        };

        var acquired = await WithDelegateAsync(
            (driverDelegate, holder) => driverDelegate.SelectTriggersToAcquire(holder, criteria, cancellationToken).AsTask(),
            cancellationToken);
        return acquired.Select(result => result.TriggerKey).ToList();
    }

    /// <summary>Reads <paramref name="trigger"/> the way acquisition reads the triggers it is about to fire.</summary>
    public Task<IOperableTrigger> SelectTriggerAsync(TriggerKey trigger, CancellationToken cancellationToken) =>
        WithDelegateAsync(
            async (driverDelegate, holder) => (await driverDelegate.SelectTriggers(holder, [trigger], cancellationToken)).Single(),
            cancellationToken);

    private async Task<T> WithDelegateAsync<T>(
        Func<IDriverDelegate, ConnectionAndTransactionHolder, Task<T>> use,
        CancellationToken cancellationToken)
    {
        var dbProvider = _services.GetRequiredKeyedService<IDbProvider>(QuartzRegistration.SchedulerName);
        var driverDelegate = DriverDelegate;

        await using var connection = dbProvider.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var holder = new ConnectionAndTransactionHolder(connection, null);
        return await use(driverDelegate, holder);
    }

    private static async Task MigrateAsync(
        TestDatabaseProvider provider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await using DbContext context = provider == TestDatabaseProvider.PostgreSql
            ? new JobsPostgreSqlDbContext(OptionsFor<JobsPostgreSqlDbContext>(configuration), DesignTimeDbContextDependencies.TenantContext)
            : new JobsSqlServerDbContext(OptionsFor<JobsSqlServerDbContext>(configuration), DesignTimeDbContextDependencies.TenantContext);
        await context.Database.MigrateAsync(cancellationToken);
    }

    // Built the way the module registers its context, so the test migrates with the runtime's options.
    private static DbContextOptions<TContext> OptionsFor<TContext>(IConfiguration configuration)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>();
        options.ConfigureModuleDbContext(configuration, JobsPersistence.ConfigureDbContextOptions);
        return options.Options;
    }

    public async ValueTask DisposeAsync()
    {
        await _scheduler.Shutdown(cancellationToken: CancellationToken.None);
        await _services.DisposeAsync();
        await Database.DisposeAsync();
    }

    private sealed class NoopHandler(string jobType) : IBackgroundJobHandler
    {
        public string JobType => jobType;

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }
}

/// <param name="Key">The trigger's name and group.</param>
/// <param name="JobType">The job type whose durable job it fires.</param>
/// <param name="SecondsAfterDue">How long after <see cref="AcquisitionTestStore.Due"/> it is due.</param>
/// <param name="ExecutionGroup">Its execution group; <see langword="null"/> for none.</param>
/// <param name="Priority">Its priority; Quartz's default is 5.</param>
internal sealed record AcquisitionTestTrigger(
    TriggerKey Key,
    string JobType,
    double SecondsAfterDue,
    string? ExecutionGroup,
    int Priority = 5)
{
    /// <summary>A trigger of <paramref name="jobType"/> as the module stores one: in its job type's group.</summary>
    public static AcquisitionTestTrigger Of(string jobType, string name, double secondsAfterDue) =>
        new(new TriggerKey(name, jobType), jobType, secondsAfterDue, jobType);
}
