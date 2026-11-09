using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Extension methods for data seeding.
/// </summary>
public static class DataSeedingExtensions
{
    // This private class is only used as a logger category
    private sealed class DataSeedingLogger { }

    /// <summary>
    /// Seeds initial user data.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task SeedInitialUserAsync(this IApplicationBuilder app)
    {
        return app.ApplicationServices.SeedInitialUserAsync();
    }

    /// <summary>
    /// Seeds initial user data.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SeedInitialUserAsync(this IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<DataSeedingLogger>();

        logger?.LogInformation("Seeding initial user data");

        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var userManager = scopedProvider.GetRequiredService<UserManager<AppUser>>();
        var userRegistrationService = scopedProvider.GetRequiredService<IUserRegistrationService>();
        var roleManagementService = scopedProvider.GetRequiredService<IRoleManagementService>();
        var dataOptions = scopedProvider.GetRequiredService<IOptions<DataOptions>>().Value;

        var seedLogger = logger != null ? (ILogger)logger : NullLogger.Instance;

        await IdentitySeed.SeedInitialUser(
            userManager,
            userRegistrationService,
            roleManagementService,
            dataOptions,
            seedLogger);

        logger?.LogInformation("Initial user data seeded successfully");
    }
}
