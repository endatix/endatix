using Microsoft.Extensions.Logging;

namespace Endatix.Framework.Modules;

/// <summary>
/// Applies EF Core migrations for a registered DbContext at application startup.
/// Register via <c>AddDbContextWithMigrations</c> (modules) or persistence setup (core contexts).
/// </summary>
public interface IDbContextMigrationContributor
{
    /// <summary>
    /// Applies EF Core migrations for a registered DbContext at application startup.
    /// </summary>
    /// <param name="scopedProvider">The scoped service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MigrateAsync(
        IServiceProvider scopedProvider,
        ILogger logger,
        CancellationToken cancellationToken = default);
}
