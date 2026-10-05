using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="Member"/>.
/// </summary>
internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");

        builder.Property(member => member.TenantId).IsRequired();

        builder.Property(member => member.Identifier)
            .HasMaxLength(DataSchemaConstants.MAX_EMAIL_LENGTH)
            .IsRequired();

        builder.Property(member => member.NormalizedIdentifier)
            .HasMaxLength(DataSchemaConstants.MAX_EMAIL_LENGTH)
            .IsRequired();
    }
}
