using Endatix.Core.Common;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="AudienceMember"/>.
/// </summary>
internal sealed class AudienceMemberConfiguration : IEntityTypeConfiguration<AudienceMember>
{
    public void Configure(EntityTypeBuilder<AudienceMember> builder)
    {
        builder.ToTable("AudienceMembers");

        builder.Property(member => member.TenantId).IsRequired();

        builder.Property(member => member.Identifier)
            .HasMaxLength(DataSchemaConstants.MAX_EMAIL_LENGTH)
            .IsRequired();
    }
}
