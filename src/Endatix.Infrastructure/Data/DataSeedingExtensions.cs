using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Extension methods for data seeding.
/// </summary>
public static class DataSeedingExtensions
{
    // This private class is only used as a logger category
    private class DataSeedingLogger { }
    
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
        
        try
        {
            logger?.LogInformation("Seeding initial user data");
            
            // Get required services
            using var scope = serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            var userManager = scopedProvider.GetRequiredService<UserManager<AppUser>>();
            var userRegistrationService = scopedProvider.GetRequiredService<IUserRegistrationService>();
            var dataOptions = scopedProvider.GetRequiredService<IOptions<DataOptions>>().Value;
            
            // Create a suitable logger for IdentitySeed (ILogger instead of ILogger<T>)
            ILogger seedLogger = logger != null ? (ILogger)logger : NullLogger.Instance;
            
            // Call the implementation in IdentitySeed
            await IdentitySeed.SeedInitialUser(
                userManager,
                userRegistrationService,
                dataOptions,
                seedLogger);
            
            logger?.LogInformation("Initial user data seeded successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while seeding initial user data");
            throw; // Rethrow for explicit seeding calls
        }
    }
} 