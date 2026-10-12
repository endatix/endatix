using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="AudienceLink"/>: one live link per membership and a unique token hash.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class AudienceLinkConfigurationSqlServer : IEntityTypeConfiguration<AudienceLink>
{
    public void Configure(EntityTypeBuilder<AudienceLink> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Links_TenantId",
            $"[{nameof(AudienceLink.TenantId)}] > 0"));

        builder.HasIndex(link => new { link.TenantId, link.FormId, link.MembershipId })
            .IsUnique()
            .HasDatabaseName(AudienceLink.UniqueConstraints.OnePerMembership)
            .HasFilter($"[{nameof(AudienceLink.IsDeleted)}] = 0");

        builder.HasIndex(link => link.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Links_TokenHash")
            .HasFilter($"[{nameof(AudienceLink.IsDeleted)}] = 0");
    }
}
