using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence.Config;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// PostgreSQL context for the Audience module. Owns migrations under
/// <c>Persistence/Migrations/PostgreSql</c>.
/// </summary>
public sealed class AudiencePostgreSqlDbContext(
    DbContextOptions<AudiencePostgreSqlDbContext> options,
    ITenantContext tenantContext)
    : AudienceDbContextBase(options, tenantContext)
{
    /// <inheritdoc />
    protected override void EnsureSupportedProvider() =>
        Database.EnsureNpgsql(typeof(AudiencePostgreSqlDbContext));

    /// <inheritdoc />
    private protected override AudienceSqlDialect SqlDialect => AudienceSqlDialect.PostgreSql;
}
