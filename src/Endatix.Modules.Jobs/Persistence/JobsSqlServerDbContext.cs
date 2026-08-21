using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// SQL Server context for the Background Jobs queue. Owns the migrations and model snapshot under
/// <c>Persistence/Migrations/SqlServer</c>.
/// </summary>
public sealed class JobsSqlServerDbContext(
    DbContextOptions<JobsSqlServerDbContext> options,
    ITenantContext tenantContext,
    EfCoreValueGeneratorFactory valueGeneratorFactory)
    : JobsDbContextBase(options, tenantContext, valueGeneratorFactory)
{
    /// <inheritdoc />
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<JobsSqlServerDbContext>(
            typeof(JobsSqlServerDbContext).Assembly);
}
