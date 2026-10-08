using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

internal sealed class AudienceLinkConfiguration : IEntityTypeConfiguration<AudienceLink>
{
    public void Configure(EntityTypeBuilder<AudienceLink> builder)
    {
        builder.ToTable("Links");
        builder.Property(link => link.TenantId).IsRequired();
        builder.Property(link => link.FormId).IsRequired();
        builder.Property(link => link.MembershipId).IsRequired();
        builder.Property(link => link.TokenHash)
            .HasMaxLength(AudienceLink.TokenHashLength)
            .IsRequired();
        builder.Property(link => link.SubmissionId);
        builder.Property(link => link.OpenedAt);
    }
}
