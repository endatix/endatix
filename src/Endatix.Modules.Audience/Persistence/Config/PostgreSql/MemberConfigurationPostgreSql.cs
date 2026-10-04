using Endatix.Modules.Audience.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="Member"/>: unique identifier per tenant.
/// </summary>
[ApplyConfigurationFor<AudiencePostgreSqlDbContext>]
internal sealed class MemberConfigurationPostgreSql : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Members_TenantId",
            $"\"{nameof(Member.TenantId)}\" > 0"));

        builder.HasIndex(member => new { member.TenantId, member.Identifier })
            .IsUnique()
            .HasDatabaseName("IX_Members_Identifier")
            .HasFilter($"\"{nameof(Member.IsDeleted)}\" = false");
    }
}
