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
        builder.Property(value => value.IsTest).IsRequired();
        builder.Property(value => value.Value)
            .HasMaxLength(PropertyValue.VALUE_MAX_LENGTH)
            .IsRequired();
        HasIndexes(builder);
        builder.HasOne<Membership>().WithMany()
            .HasForeignKey(value => new { value.TenantId, value.MembershipId, value.IsTest })
            .HasPrincipalKey(membership => new { membership.TenantId, membership.Id, membership.IsTest })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PropertyValues_Membership");
    }

    private void HasIndexes(EntityTypeBuilder<PropertyValue> builder)
    {
        // IsTest follows MembershipId, so the cell stays unique per membership and Property; leading with the foreign
        // key's columns makes this index serve FK_PropertyValues_Membership too.
        builder.HasIndex(value => new { value.TenantId, value.MembershipId, value.IsTest, value.PropertyId })
            .IsUnique()
            .HasDatabaseName(PropertyValue.UniqueConstraints.CellPerMembership)
            .HasFilter(dialect.ActiveRowsFilter);

        builder.HasIndex(value => new { value.TenantId, value.PropertyId, value.IsTest })
            .HasDatabaseName("IX_PropertyValues_Property")
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
