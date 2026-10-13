using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="AudienceLink"/>: one live link per membership and a unique token hash.
/// </summary>
internal sealed class AudienceLinkConfiguration(AudienceSqlDialect dialect) : IEntityTypeConfiguration<AudienceLink>
{
    public void Configure(EntityTypeBuilder<AudienceLink> builder)
    {
        builder.ToTable("Links", table => table.HasCheckConstraint(
            "CK_Links_TenantId", dialect.PositiveTenantIdCheck));
        builder.Property(link => link.TenantId).IsRequired();
        builder.Property(link => link.FormId).IsRequired();
        builder.Property(link => link.MembershipId).IsRequired();
        builder.Property(link => link.TokenHash)
            .HasMaxLength(AudienceLink.TokenHashLength)
            .IsRequired();
        builder.Property(link => link.SubmissionId);
        builder.Property(link => link.OpenedAt);
        HasIndexes(builder);
    }

    private void HasIndexes(EntityTypeBuilder<AudienceLink> builder)
    {
        builder.HasIndex(link => new { link.TenantId, link.FormId, link.MembershipId })
            .IsUnique()
            .HasDatabaseName(AudienceLink.UniqueConstraints.OnePerMembership)
            .HasFilter(dialect.ActiveRowsFilter);

        builder.HasIndex(link => link.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Links_TokenHash")
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
