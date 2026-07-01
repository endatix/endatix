using Endatix.Framework.Modules;
using Microsoft.EntityFrameworkCore;
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

        await DbContextMigrationRunner.MigrateAsync<TContext>(scopedProvider, logger, cancellationToken);
    }
}
