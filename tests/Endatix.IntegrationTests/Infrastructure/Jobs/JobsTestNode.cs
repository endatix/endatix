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

    public Task StartAsync(CancellationToken cancellationToken) => _host.StartAsync(cancellationToken);

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

    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
        {
            try
            {
                await _host.StopAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // A node that failed to start has nothing to stop.
            }
        }

        _host.Dispose();
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
