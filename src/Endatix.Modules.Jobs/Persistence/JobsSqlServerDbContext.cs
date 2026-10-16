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
    ITenantContext tenantContext)
    : JobsDbContextBase(options, tenantContext)
{
    /// <inheritdoc />
    /// <remarks>SQL Server pads the shorter value with spaces when it compares, so "k1 " equals "k1".</remarks>
    public override bool IgnoresTrailingSpacesInKeys => true;

    /// <inheritdoc />
    protected override void EnsureSupportedProvider() =>
        Database.EnsureSqlServer(typeof(JobsSqlServerDbContext));

    /// <inheritdoc />
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<JobsSqlServerDbContext>(typeof(JobsSqlServerDbContext).Assembly);
}
