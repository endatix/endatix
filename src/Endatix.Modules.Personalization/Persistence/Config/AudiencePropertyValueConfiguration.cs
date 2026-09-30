using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudiencePropertyValue"/>.
/// </summary>
internal sealed class AudiencePropertyValueConfiguration : IEntityTypeConfiguration<AudiencePropertyValue>
{
    public void Configure(EntityTypeBuilder<AudiencePropertyValue> builder)
    {
        builder.ToTable("AudiencePropertyValues");

        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.AudienceMembershipId).IsRequired();
        builder.Property(value => value.AudiencePropertyId).IsRequired();
        builder.Property(value => value.Value).IsRequired();

        builder.HasIndex(value => value.AudienceMembershipId)
            .HasDatabaseName("IX_AudiencePropertyValues_Membership");
    }
}