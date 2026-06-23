using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data.Abstractions;

/// <summary>
/// Optional database migration contributor for module-owned DbContext instances.
/// </summary>
public interface IDbContextMigrationContributor
{
    Task MigrateAsync(
        IServiceProvider scopedProvider,
        ILogger logger,
        CancellationToken cancellationToken = default);
}
