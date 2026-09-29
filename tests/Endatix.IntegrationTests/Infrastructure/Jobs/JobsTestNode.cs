using Endatix.Core.Abstractions;
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
    /// Builds a node. It applies the jobs migrations before its scheduler starts, as a host with automatic
    /// migrations on does, unless <paramref name="migrate"/> is off.
    /// </summary>
    public static JobsTestNode Create(
        string connectionString,
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
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql",
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

    public Task StartAsync(CancellationToken cancellationToken) => _host.StartAsync(cancellationToken);

    /// <summary>A graceful stop, as a host shutting down does.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stopped = true;
        await _host.StopAsync(cancellationToken);
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
