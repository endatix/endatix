using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// PostgreSQL context for the Background Jobs queue. Owns the migrations and model snapshot under
/// <c>Persistence/Migrations/PostgreSql</c>.
/// </summary>
public sealed class JobsPostgreSqlDbContext(
    DbContextOptions<JobsPostgreSqlDbContext> options,
    ITenantContext tenantContext,
    EfCoreValueGeneratorFactory valueGeneratorFactory)
    : JobsDbContextBase(options, tenantContext, valueGeneratorFactory)
{
    /// <inheritdoc />
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<JobsPostgreSqlDbContext>(
            typeof(JobsPostgreSqlDbContext).Assembly);
}
