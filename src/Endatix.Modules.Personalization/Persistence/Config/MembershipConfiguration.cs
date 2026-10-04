using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="Membership"/>.
/// </summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");

        builder.Property(membership => membership.TenantId).IsRequired();
        builder.Property(membership => membership.FormId).IsRequired();
        builder.Property(membership => membership.MemberId).IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.FormId })
            .HasDatabaseName("IX_Memberships_Form");
    }
}
