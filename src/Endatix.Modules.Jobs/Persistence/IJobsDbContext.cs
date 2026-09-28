using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// The Background Jobs queue, as consumers see it.
/// </summary>
/// <remarks>
/// Runtime code depends on this rather than on a concrete context, so nothing outside
/// <see cref="JobsPersistence"/> branches on the active database provider. Exactly one implementation
/// — <see cref="JobsPostgreSqlDbContext"/> — is registered at startup.
/// </remarks>
public interface IJobsDbContext : ITenantDbContext
{
    DbSet<BackgroundJob> BackgroundJobs { get; }

    /// <summary>
    /// The context's database, through which enqueueing opens the transaction the scheduler joins.
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
