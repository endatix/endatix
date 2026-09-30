using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// PostgreSQL context for Personalization. Owns migrations under
/// <c>Persistence/Migrations/PostgreSql</c>.
/// </summary>
public sealed class PersonalizationPostgreSqlDbContext(
    DbContextOptions<PersonalizationPostgreSqlDbContext> options,
    ITenantContext tenantContext)
    : PersonalizationDbContextBase(options, tenantContext)
{
    protected override void ApplyProviderConfigurations(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFor<PersonalizationPostgreSqlDbContext>(
            typeof(PersonalizationPostgreSqlDbContext).Assembly);
}
