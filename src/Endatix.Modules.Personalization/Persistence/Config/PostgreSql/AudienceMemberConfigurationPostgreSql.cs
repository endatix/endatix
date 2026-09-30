using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudienceMember"/>: unique identifier per tenant.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class AudienceMemberConfigurationPostgreSql : IEntityTypeConfiguration<AudienceMember>
{
    public void Configure(EntityTypeBuilder<AudienceMember> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AudienceMembers_TenantId",
            $"\"{nameof(AudienceMember.TenantId)}\" > 0"));

        builder.HasIndex(member => new { member.TenantId, member.Identifier })
            .IsUnique()
            .HasDatabaseName("IX_AudienceMembers_Identifier")
            .HasFilter($"\"{nameof(AudienceMember.IsDeleted)}\" = false");
    }
}
