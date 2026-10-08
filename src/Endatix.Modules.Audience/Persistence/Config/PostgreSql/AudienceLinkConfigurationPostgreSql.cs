using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<AudiencePostgreSqlDbContext>]
internal sealed class AudienceLinkConfigurationPostgreSql : IEntityTypeConfiguration<AudienceLink>
{
    public void Configure(EntityTypeBuilder<AudienceLink> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Links_TenantId",
            $"\"{nameof(AudienceLink.TenantId)}\" > 0"));

        builder.HasIndex(link => new { link.TenantId, link.FormId, link.MembershipId })
            .IsUnique()
            .HasDatabaseName("IX_Links_Membership")
            .HasFilter($"\"{nameof(AudienceLink.IsDeleted)}\" = false");

        builder.HasIndex(link => link.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Links_TokenHash")
            .HasFilter($"\"{nameof(AudienceLink.IsDeleted)}\" = false");
    }
}
