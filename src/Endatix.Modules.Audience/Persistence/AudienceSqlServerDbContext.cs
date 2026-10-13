using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence.Config;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// SQL Server context for the Audience module. Owns migrations under
/// <c>Persistence/Migrations/SqlServer</c>.
/// </summary>
public sealed class AudienceSqlServerDbContext(
    DbContextOptions<AudienceSqlServerDbContext> options,
    ITenantContext tenantContext)
    : AudienceDbContextBase(options, tenantContext)
{
    /// <inheritdoc />
    protected override void EnsureSupportedProvider() =>
        Database.EnsureSqlServer(typeof(AudienceSqlServerDbContext));

    /// <inheritdoc />
    private protected override AudienceSqlDialect SqlDialect => AudienceSqlDialect.SqlServer;
}
