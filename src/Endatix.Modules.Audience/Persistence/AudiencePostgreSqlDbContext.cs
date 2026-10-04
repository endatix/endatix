using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
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
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<AudiencePostgreSqlDbContext>(
            typeof(AudiencePostgreSqlDbContext).Assembly);
}
