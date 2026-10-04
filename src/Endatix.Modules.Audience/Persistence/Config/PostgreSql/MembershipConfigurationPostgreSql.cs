using Endatix.Modules.Audience.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="Membership"/>: one membership per person per form.
/// </summary>
[ApplyConfigurationFor<AudiencePostgreSqlDbContext>]
internal sealed class MembershipConfigurationPostgreSql : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Memberships_TenantId",
            $"\"{nameof(Membership.TenantId)}\" > 0"));

        builder.HasIndex(membership => new
            {
                membership.TenantId,
                membership.FormId,
                membership.MemberId,
            })
            .IsUnique()
            .HasDatabaseName("IX_Memberships_Member")
            .HasFilter($"\"{nameof(Membership.IsDeleted)}\" = false");
    }
}
