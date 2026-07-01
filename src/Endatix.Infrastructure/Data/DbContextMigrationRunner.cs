using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Shared EF Core migration execution for startup migration phases.
/// </summary>
internal static class DbContextMigrationRunner
{
    /// <summary>
    /// Applies EF Core migrations for a registered DbContext at application startup.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="scopedProvider">The scoped service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task MigrateAsync<TContext>(
        IServiceProvider scopedProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        DbContext? dbContext = scopedProvider.GetService<TContext>();
        if (dbContext is null)
        {
            var contextName = typeof(TContext).Name;
            var message =
                $"{contextName} is not registered in the service provider. " +
                "Startup migrations cannot run without the DbContext registration for the active provider.";
            logger.LogError(
                "{DbContextName} is not registered in the service provider. Startup migrations cannot run without the DbContext registration for the active provider.",
                contextName);
            throw new InvalidOperationException(message);
        }

        var migrations = dbContext.Database.GetMigrations();
        if (!migrations.Any())
        {
            var contextName = typeof(TContext).Name;
            var message =
                $"No EF Core migrations are registered for {contextName}. " +
                "Auto-migration cannot create the database schema for the active provider. " +
                "Generate provider-specific migrations before enabling startup migrations " +
                "(see module README; Reporting SQL Server: https://github.com/endatix/endatix/issues/813).";
            logger.LogError(
                "No EF Core migrations are registered for {DbContextName}. Auto-migration cannot create the database schema for the active provider. Generate provider-specific migrations before enabling startup migrations (see module README; Reporting SQL Server: https://github.com/endatix/endatix/issues/813).",
                contextName);
            throw new InvalidOperationException(message);
        }

        var startTime = Stopwatch.GetTimestamp();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var elapsedTime = Stopwatch.GetElapsedTime(startTime);
        logger.LogWarning(
            "Database migrations applied for {DbContextName}. Took: {ElapsedMs} ms.",
            typeof(TContext).Name,
            elapsedTime.TotalMilliseconds);
    }
}
