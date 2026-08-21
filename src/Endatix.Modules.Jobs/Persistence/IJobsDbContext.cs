using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// The Background Jobs queue, as consumers see it.
/// </summary>
/// <remarks>
/// Runtime code depends on this rather than on a concrete context, so nothing outside
/// <see cref="JobsPersistence"/> branches on the active database provider. Exactly one implementation
/// — <see cref="JobsPostgreSqlDbContext"/> or <see cref="JobsSqlServerDbContext"/> — is registered at
/// startup.
/// </remarks>
public interface IJobsDbContext : ITenantDbContext
{
    DbSet<BackgroundJob> BackgroundJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
