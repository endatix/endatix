using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Applies EF Core migrations for core platform DbContexts at application startup.
/// </summary>
internal static class CoreDbContextMigrationService
{
    /// <summary>
    /// Applies EF Core migrations for core platform DbContexts at application startup.
    /// </summary>
    /// <param name="scopedProvider">The scoped service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateCoreDbContextsAsync(
        IServiceProvider scopedProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await DbContextMigrationRunner.MigrateAsync<AppDbContext>(scopedProvider, logger, cancellationToken);
        await DbContextMigrationRunner.MigrateAsync<AppIdentityDbContext>(scopedProvider, logger, cancellationToken);
    }
}
