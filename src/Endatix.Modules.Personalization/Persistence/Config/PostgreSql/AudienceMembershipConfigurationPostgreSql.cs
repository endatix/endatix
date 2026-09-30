using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudienceMembership"/>: one membership per person per form.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
internal sealed class AudienceMembershipConfigurationPostgreSql : IEntityTypeConfiguration<AudienceMembership>
{
    public void Configure(EntityTypeBuilder<AudienceMembership> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AudienceMemberships_TenantId",
            $"\"{nameof(AudienceMembership.TenantId)}\" > 0"));

        builder.HasIndex(membership => new
            {
                membership.TenantId,
                membership.FormId,
                membership.AudienceMemberId,
            })
            .IsUnique()
            .HasDatabaseName("IX_AudienceMemberships_Member")
            .HasFilter($"\"{nameof(AudienceMembership.IsDeleted)}\" = false");
    }
}
