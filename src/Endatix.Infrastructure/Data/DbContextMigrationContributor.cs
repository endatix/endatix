using System.Diagnostics;
using Endatix.Framework.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Applies EF migrations for a registered DbContext at application startup.
/// </summary>
public sealed class DbContextMigrationContributor<TContext> : IDbContextMigrationContributor
    where TContext : DbContext
{
    private readonly Func<IServiceProvider, bool>? _shouldMigrate;

    public DbContextMigrationContributor(Func<IServiceProvider, bool>? shouldMigrate = null)
    {
        _shouldMigrate = shouldMigrate;
    }

    public async Task MigrateAsync(
        IServiceProvider scopedProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (_shouldMigrate is not null && !_shouldMigrate(scopedProvider))
        {
            return;
        }

        var dbContext = scopedProvider.GetService<TContext>();
        if (dbContext is null)
        {
            return;
        }

        var migrations = dbContext.Database.GetMigrations();
        if (!migrations.Any())
        {
            var message =
                $"No EF Core migrations are registered for {typeof(TContext).Name}. " +
                "Auto-migration cannot create the database schema for the active provider. " +
                "Generate provider-specific migrations before enabling startup migrations " +
                "(see module README; Reporting SQL Server: https://github.com/endatix/endatix/issues/813).";
            logger.LogError(message);
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
