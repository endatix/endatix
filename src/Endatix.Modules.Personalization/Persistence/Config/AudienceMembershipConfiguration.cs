using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudienceMembership"/>.
/// </summary>
internal sealed class AudienceMembershipConfiguration : IEntityTypeConfiguration<AudienceMembership>
{
    public void Configure(EntityTypeBuilder<AudienceMembership> builder)
    {
        builder.ToTable("AudienceMemberships");

        builder.Property(membership => membership.TenantId).IsRequired();
        builder.Property(membership => membership.FormId).IsRequired();
        builder.Property(membership => membership.AudienceMemberId).IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.FormId })
            .HasDatabaseName("IX_AudienceMemberships_Form");
    }
}
