using Endatix.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Extension methods for database migrations.
/// </summary>
public static class DatabaseMigrationExtensions
{
    private class MigrationLogger { }

    /// <summary>
    /// Applies database migrations for core DbContexts, then all registered
    /// <see cref="IDbContextMigrationContributor"/> instances (module opt-in).
    /// </summary>
    public static async Task ApplyDbMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        ILogger logger = serviceProvider.GetRequiredService<ILogger<MigrationLogger>>();
        logger.LogDebug("{Operation} operation started", nameof(ApplyDbMigrationsAsync));

        try
        {
            var scopedProvider = scope.ServiceProvider;

            await CoreDbContextMigrationService.MigrateCoreDbContextsAsync(scopedProvider, logger, cancellationToken);

            var contributors =
                scopedProvider.GetServices<IDbContextMigrationContributor>();

            foreach (var contributor in contributors)
            {
                await contributor.MigrateAsync(scopedProvider, logger, cancellationToken);
            }

            logger.LogDebug("{Operation} operation executed successfully", nameof(ApplyDbMigrationsAsync));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            throw;
        }
    }
}
