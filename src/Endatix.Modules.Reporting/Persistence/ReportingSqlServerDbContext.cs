using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// SQL Server context for the Reporting export read model. Owns the migrations and model snapshot under
/// <c>Persistence/Migrations/SqlServer</c>.
/// </summary>
public sealed class ReportingSqlServerDbContext(
    DbContextOptions<ReportingSqlServerDbContext> options,
    ITenantContext tenantContext)
    : ReportingDbContextBase(options, tenantContext)
{
    /// <inheritdoc />
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<ReportingSqlServerDbContext>(
            typeof(ReportingSqlServerDbContext).Assembly);
}
