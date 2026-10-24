using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="PropertyValue"/>: one cell per property on a membership.
/// </summary>
internal sealed class PropertyValueConfiguration(AudienceSqlDialect dialect) : IEntityTypeConfiguration<PropertyValue>
{
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.ToTable("PropertyValues", table => table.HasCheckConstraint(
            "CK_PropertyValues_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.MembershipId).IsRequired();
        builder.Property(value => value.PropertyId).IsRequired();
        builder.Property(value => value.IsTest).IsRequired().HasDefaultValue(false);
        builder.Property(value => value.Value)
            .HasMaxLength(PropertyValue.VALUE_MAX_LENGTH)
            .IsRequired();
        HasIndexes(builder);
    }

    private void HasIndexes(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.HasIndex(value => new { value.TenantId, value.MembershipId, value.PropertyId })
            .IsUnique()
            .HasDatabaseName(PropertyValue.UniqueConstraints.CellPerMembership)
            .HasFilter(dialect.ActiveRowsFilter);

        builder.HasIndex(value => new { value.TenantId, value.PropertyId, value.IsTest })
            .HasDatabaseName("IX_PropertyValues_Property")
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
