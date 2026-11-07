using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Core.Infrastructure;
using Endatix.Modules.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// One scheduler node: a generic host running only the Jobs module against a <see cref="JobsTestDatabase"/>.
/// Several can run in one process, as the nodes of a cluster.
/// </summary>
internal sealed class JobsTestNode : IAsyncDisposable
{
    private readonly IHost _host;
    private bool _stopped;
    private Exception? _startFailure;

    private JobsTestNode(IHost host) => _host = host;

    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Builds a node on whatever <paramref name="database"/> points at, including a server that is not there.
    /// </summary>
    public static JobsTestNode Create(
        JobsTestNodeDatabase database,
        IDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        bool migrate = true)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = Environments.Staging,
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
            ["ConnectionStrings:DefaultConnection_DbProvider"] = database.Provider.ToString(),
            // Short cluster timings, so a stopped node's work is recovered within a test's patience.
            ["Endatix:BackgroundJobs:IdleWaitTimeSeconds"] = "1",
            ["Endatix:BackgroundJobs:Clustering:CheckinIntervalSeconds"] = "1",
            ["Endatix:BackgroundJobs:Clustering:CheckinMisfireThresholdSeconds"] = "2",
        });
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        var services = builder.Services;
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IIdGenerator<long>, SnowflakeIdGenerator>();
        services.AddScoped<ITenantContext>(_ => IntegrationTenantContext.Bypass);

        // Registered before the module, so the migrations finish before the scheduler validates its tables.
        if (migrate)
        {
            services.AddHostedService<MigrateJobsSchema>();
        }

        JobsModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, builder.Configuration));
        configureServices?.Invoke(services);

        return new JobsTestNode(builder.Build());
    }

    /// <summary>
    /// Builds a node on <paramref name="database"/>, with that database's provider. It applies the jobs migrations
    /// before its scheduler starts, as a host with automatic migrations on does, unless <paramref name="migrate"/>
    /// is off.
    /// </summary>
    public static JobsTestNode Create(
        JobsTestDatabase database,
        IDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        bool migrate = true) =>
        Create(new JobsTestNodeDatabase(database.Provider, database.ConnectionString), settings, configureServices, migrate);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _host.StartAsync(cancellationToken);
        }
        catch (Exception failure)
        {
            _startFailure = failure;
            throw;
        }
    }

    /// <summary>
    /// Starts the node and returns it; a start that fails stops and disposes the node before its failure reaches the
    /// caller, which never gets a node to dispose.
    /// </summary>
    public Task<JobsTestNode> StartedAsync(CancellationToken cancellationToken) =>
        FailedHostStart.DisposeOnFailureAsync(
            async () =>
            {
                await StartAsync(cancellationToken);
                return this;
            },
            DisposeAsync);

    /// <summary>A graceful stop, as a host shutting down does.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stopped = true;
        await _host.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the node the way a crash does, as far as the rest of the cluster can tell: its scheduler lets go of
    /// running jobs without waiting, and nothing of the host's own shutdown runs.
    /// </summary>
    public async Task KillAsync()
    {
        var scheduler = await Services
            .GetRequiredKeyedService<Quartz.ISchedulerFactory>(Endatix.Modules.Jobs.Runtime.QuartzRegistration.SchedulerName)
            .GetScheduler();
        await scheduler.Shutdown(waitForJobsToComplete: false);
        _stopped = true;
        _host.Dispose();
    }

    /// <summary>
    /// Stops the node unless it was stopped already, and disposes it whatever the stop did. When the stop fails, its
    /// failure is thrown and a failing disposal is reported as a test warning.
    /// </summary>
    /// <remarks>
    /// A node whose start failed is stopped and disposed too: the services that started before the failure are still
    /// running, and .NET disposes a host whose start failed without stopping it. When either fails, an
    /// <see cref="AggregateException"/> is thrown with the start's failure first. A test that expected the start to
    /// fail still fails on a broken teardown, and under <c>await using var node = Create(); await node.StartAsync(ct);</c>
    /// a test whose start failed unexpectedly still sees that failure, which a throw of the teardown's alone would
    /// replace.
    /// </remarks>
    public ValueTask DisposeAsync() =>
        _startFailure is null
            ? DisposalFailures.DisposeInOrderAsync(
                StopOnceAsync, DisposeHostAsync, "the node's disposal after its failed stop")
            : DisposeAfterFailedStartAsync(_startFailure);

    private async ValueTask DisposeAfterFailedStartAsync(Exception startFailure)
    {
        List<Exception> failures = [startFailure];
        await RecordFailureAsync(StopOnceAsync, failures);
        await RecordFailureAsync(DisposeHostAsync, failures);
        if (failures.Count > 1)
        {
            throw new AggregateException("The node's start failed, and so did its teardown.", failures);
        }
    }

    private static async ValueTask RecordFailureAsync(Func<ValueTask> step, List<Exception> failures)
    {
        try
        {
            await step();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    // A second disposal does not stop the node again, so a stop that failed fails its test once.
    private ValueTask StopOnceAsync()
    {
        if (_stopped)
        {
            return ValueTask.CompletedTask;
        }

        _stopped = true;
        return new ValueTask(_host.StopAsync(CancellationToken.None));
    }

    private ValueTask DisposeHostAsync()
    {
        _host.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class MigrateJobsSchema(IServiceProvider services, ILogger<MigrateJobsSchema> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = services.CreateScope();
            foreach (var contributor in scope.ServiceProvider.GetServices<IDbContextMigrationContributor>())
            {
                await contributor.MigrateAsync(scope.ServiceProvider, logger, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>The provider and connection string a <see cref="JobsTestNode"/> runs against.</summary>
/// <remarks>A class, not a record, so no assertion message or log prints the connection string.</remarks>
internal sealed class JobsTestNodeDatabase(TestDatabaseProvider provider, string connectionString)
{
    public TestDatabaseProvider Provider { get; } = provider;

    public string ConnectionString { get; } = connectionString;
}
