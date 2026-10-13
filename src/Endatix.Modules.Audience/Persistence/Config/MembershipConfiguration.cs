using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="Membership"/>: one membership per person per form.
/// </summary>
internal sealed class MembershipConfiguration(AudienceSqlDialect dialect) : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships", table => table.HasCheckConstraint(
            "CK_Memberships_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(membership => membership.TenantId).IsRequired();
        builder.Property(membership => membership.FormId).IsRequired();
        builder.Property(membership => membership.MemberId).IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.FormId })
            .HasDatabaseName("IX_Memberships_Form")
            .HasFilter(dialect.ActiveRowsFilter);

        builder.HasIndex(membership => new { membership.TenantId, membership.FormId, membership.MemberId })
            .IsUnique()
            .HasDatabaseName(Membership.UniqueConstraints.MemberPerForm)
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
