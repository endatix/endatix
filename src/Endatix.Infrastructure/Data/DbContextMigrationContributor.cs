using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Applies EF migrations for a registered module DbContext at application startup.
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

        logger.LogWarning("Applying database migrations for {DbContextName}", typeof(TContext).Name);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
