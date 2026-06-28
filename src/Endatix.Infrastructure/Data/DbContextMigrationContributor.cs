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

        var startTime = Stopwatch.GetTimestamp();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var elapsedTime = Stopwatch.GetElapsedTime(startTime);
        logger.LogWarning(
            "Database migrations applied for {DbContextName}. Took: {ElapsedMs} ms.",
            typeof(TContext).Name,
            elapsedTime.TotalMilliseconds);
    }
}
