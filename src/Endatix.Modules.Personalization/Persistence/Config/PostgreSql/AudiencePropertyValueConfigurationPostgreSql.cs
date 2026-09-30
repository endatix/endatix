using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudiencePropertyValue"/>: one cell per property on a membership.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class AudiencePropertyValueConfigurationPostgreSql : IEntityTypeConfiguration<AudiencePropertyValue>
{
    public void Configure(EntityTypeBuilder<AudiencePropertyValue> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AudiencePropertyValues_TenantId",
            $"\"{nameof(AudiencePropertyValue.TenantId)}\" > 0"));

        builder.HasIndex(value => new { value.AudienceMembershipId, value.AudiencePropertyId })
            .IsUnique()
            .HasDatabaseName("IX_AudiencePropertyValues_Cell")
            .HasFilter($"\"{nameof(AudiencePropertyValue.IsDeleted)}\" = false");
    }
}
