using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// A hosted service that seeds initial data at application startup.
/// </summary>
public class DataSeedingService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSeedingService> _logger;
    private readonly DataOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSeedingService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The data options.</param>
    /// <param name="logger">The logger.</param>
    public DataSeedingService(
        IServiceProvider serviceProvider,
        IOptions<DataOptions> options,
        ILogger<DataSeedingService> logger)
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
        if (!_options.SeedSampleData)
        {
            _logger.LogDebug("{Operation} operation skipped because automatic data seeding is disabled", "SeedingSampleData");
            return;
        }

        _logger.LogInformation("Seeding initial application data");
        try
        {
            // Get required services
            using var scope = _serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var userRegistrationService = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();

            // Call the identity seed method
            await IdentitySeed.SeedInitialUser(
                userManager,
                userRegistrationService,
                _options,
                _logger);

            _logger.LogInformation("Initial data seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding initial data");
            // Don't rethrow - we don't want to prevent application startup
        }
    }

    /// <summary>
    /// Called when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}