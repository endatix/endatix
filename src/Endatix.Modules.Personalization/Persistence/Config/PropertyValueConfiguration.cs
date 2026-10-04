using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="PropertyValue"/>.
/// </summary>
internal sealed class PropertyValueConfiguration : IEntityTypeConfiguration<PropertyValue>
{
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.ToTable("PropertyValues");

        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.MembershipId).IsRequired();
        builder.Property(value => value.PropertyId).IsRequired();
        builder.Property(value => value.Value).IsRequired();

        builder.HasIndex(value => value.MembershipId)
            .HasDatabaseName("IX_PropertyValues_Membership");
    }
}