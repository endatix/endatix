using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="Member"/>: unique identifier per tenant, compared by code point.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class MemberConfigurationSqlServer : IEntityTypeConfiguration<Member>
{
    /// <summary>
    /// External ids keep their case as match keys, and SQL Server's default collations ignore case, so the
    /// match key compares by code point, as PostgreSQL's default collation does. Emails arrive normalized.
    /// </summary>
    private const string MatchKeyCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.Property(member => member.NormalizedIdentifier)
            .UseCollation(MatchKeyCollation);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Members_TenantId",
            $"[{nameof(Member.TenantId)}] > 0"));

        builder.HasIndex(member => new { member.TenantId, member.NormalizedIdentifier })
            .IsUnique()
            .HasDatabaseName(Member.UniqueConstraints.IdentifierPerTenant)
            .HasFilter($"[{nameof(Member.IsDeleted)}] = 0");
    }
}
