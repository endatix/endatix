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
        var logger = serviceProvider.GetService<ILogger<MigrationLogger>>();
        logger?.LogDebug("{Operation} operation started", nameof(ApplyDbMigrationsAsync));
        
        try
        {
            // Get all registered DbContext types
            var dbContexts = serviceProvider.GetServices<DbContext>();
            
            foreach (var dbContext in dbContexts)
            {
                var contextType = dbContext.GetType();
                logger?.LogInformation("Applying migrations for {DbContextType}", contextType.Name);
                
                try
                {
                    await dbContext.Database.MigrateAsync();
                    logger?.LogInformation("Successfully applied migrations for {DbContextType}", contextType.Name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error applying migrations for {DbContextType}", contextType.Name);
                    throw; // Rethrow to allow the caller to handle the error
                }
            }
            
            logger?.LogDebug("{Operation} operation executed successfully", nameof(ApplyDbMigrationsAsync));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while applying database migrations");
            throw; // Rethrow for explicit migration calls
        }
    }
} 