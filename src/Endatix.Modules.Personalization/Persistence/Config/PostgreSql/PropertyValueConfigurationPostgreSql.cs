using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="PropertyValue"/>: one cell per property on a membership.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class PropertyValueConfigurationPostgreSql : IEntityTypeConfiguration<PropertyValue>
{
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_PropertyValues_TenantId",
            $"\"{nameof(PropertyValue.TenantId)}\" > 0"));

        builder.HasIndex(value => new { value.MembershipId, value.PropertyId })
            .IsUnique()
            .HasDatabaseName("IX_PropertyValues_Cell")
            .HasFilter($"\"{nameof(PropertyValue.IsDeleted)}\" = false");
    }
}
