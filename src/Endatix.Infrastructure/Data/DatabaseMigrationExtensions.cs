using System.Diagnostics;
using Ardalis.GuardClauses;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Extension methods for database migrations.
/// </summary>
public static class DatabaseMigrationExtensions
{
    // This internal class is only used as a logger category
    private class MigrationLogger { }

    /// <summary>
    /// Applies database migrations for all registered DbContext types.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ApplyDbMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = serviceProvider.GetRequiredService<ILogger<MigrationLogger>>();
        logger.LogDebug("{Operation} operation started", nameof(ApplyDbMigrationsAsync));

        try
        {
            var scopedProvider = scope.ServiceProvider;

            using var appDbContext = scopedProvider.GetRequiredService<AppDbContext>();
            await ApplyMigrationForContextAsync(appDbContext, logger);

            using var identityDbContext = scopedProvider.GetRequiredService<AppIdentityDbContext>();
            await ApplyMigrationForContextAsync(identityDbContext, logger);
            
            logger?.LogDebug("{Operation} operation executed successfully", nameof(ApplyDbMigrationsAsync));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while applying database migrations");
            throw; // Rethrow for explicit migration calls
        }
    }

    private static async Task ApplyMigrationForContextAsync<T>(T dbContext, ILogger logger) where T : DbContext
    {
        Guard.Against.Null(dbContext);
        Guard.Against.Null(logger);

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            var startTime = Stopwatch.GetTimestamp();
            logger.LogInformation("💽 Applying database migrations for {dbContextName}", typeof(T).Name);

            await dbContext.Database.MigrateAsync();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);
            logger.LogInformation("💽 Database migrations applied for {dbContextName}. Took: {elapsedTime} ms.", typeof(T).Name, elapsedTime.TotalMilliseconds);
        }
    }
}