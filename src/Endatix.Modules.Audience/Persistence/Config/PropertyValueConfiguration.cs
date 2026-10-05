using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

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
        builder.Property(value => value.Value)
            .HasMaxLength(PropertyValue.VALUE_MAX_LENGTH)
            .IsRequired();
    }
}