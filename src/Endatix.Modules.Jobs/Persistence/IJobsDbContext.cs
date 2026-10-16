using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// The Background Jobs queue, as consumers see it.
/// </summary>
/// <remarks>
/// Runtime code depends on this rather than on a concrete context, so nothing outside
/// <see cref="JobsPersistence"/> branches on the active database provider. Exactly one implementation is
/// registered at startup: <see cref="JobsPostgreSqlDbContext"/> on PostgreSQL, <see cref="JobsSqlServerDbContext"/>
/// on SQL Server.
/// </remarks>
public interface IJobsDbContext : ITenantDbContext
{
    DbSet<BackgroundJob> BackgroundJobs { get; }

    /// <summary>
    /// The context's database, through which enqueueing opens the transaction the scheduler joins.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>Lets enqueueing drop rows a failed insert left tracked before it tries again.</summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Whether the database ignores trailing spaces when it compares dedup keys, so that a key ending in a space
    /// matches the key without it in the unique index while the queue's own lookup tells them apart.
    /// </summary>
    bool IgnoresTrailingSpacesInKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
