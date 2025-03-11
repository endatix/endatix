using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// A hosted service that applies database migrations at application startup.
/// </summary>
public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly DataOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMigrationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The data options.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseMigrationService(
        IServiceProvider serviceProvider,
        IOptions<DataOptions> options,
        ILogger<DatabaseMigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Called when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAutoMigrations)
        {
            _logger.LogInformation("Automatic database migrations are disabled");
            return;
        }

        _logger.LogInformation("Applying database migrations at startup");
        try
        {
            await _serviceProvider.ApplyDbMigrationsAsync();
            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while applying database migrations");
            // Don't rethrow - we don't want to prevent application startup
            // Applications can check migration status in health checks
        }
    }

    /// <summary>
    /// Called when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}