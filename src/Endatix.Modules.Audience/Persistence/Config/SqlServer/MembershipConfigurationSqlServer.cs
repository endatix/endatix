using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="Membership"/>: one membership per person per form.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class MembershipConfigurationSqlServer : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Memberships_TenantId",
            $"[{nameof(Membership.TenantId)}] > 0"));

        builder.HasIndex(membership => new { membership.TenantId, membership.FormId })
            .HasDatabaseName("IX_Memberships_Form")
            .HasFilter($"[{nameof(Membership.IsDeleted)}] = 0");

        builder.HasIndex(membership => new
            {
                membership.TenantId,
                membership.FormId,
                membership.MemberId,
            })
            .IsUnique()
            .HasDatabaseName(Membership.UniqueConstraints.MemberPerForm)
            .HasFilter($"[{nameof(Membership.IsDeleted)}] = 0");
    }
}
