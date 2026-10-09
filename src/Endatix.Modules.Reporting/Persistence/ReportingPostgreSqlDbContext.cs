using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// PostgreSQL context for the Reporting export read model. Owns the migrations and model snapshot under
/// <c>Persistence/Migrations/PostgreSql</c>.
/// </summary>
public sealed class ReportingPostgreSqlDbContext(
    DbContextOptions<ReportingPostgreSqlDbContext> options,
    ITenantContext tenantContext)
    : ReportingDbContextBase(options, tenantContext)
{
    /// <inheritdoc />
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<ReportingPostgreSqlDbContext>(
            typeof(ReportingPostgreSqlDbContext).Assembly);
}
