using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="Member"/>: unique identifier per tenant.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
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
