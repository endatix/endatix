using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="Member"/>: a unique identifier per tenant, compared by code point.
/// </summary>
internal sealed class MemberConfiguration(AudienceSqlDialect dialect) : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members", table => table.HasCheckConstraint(
            "CK_Members_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(member => member.TenantId).IsRequired();

        builder.Property(member => member.Identifier)
            .HasMaxLength(DataSchemaConstants.MAX_EMAIL_LENGTH)
            .IsRequired();

        ConfigureMatchKey(builder);
    }

    private void ConfigureMatchKey(EntityTypeBuilder<Member> builder)
    {
        var matchKey = builder.Property(member => member.NormalizedIdentifier)
            .HasMaxLength(DataSchemaConstants.MAX_EMAIL_LENGTH)
            .IsRequired();
        if (dialect.MatchKeyCollation is { } collation)
        {
            matchKey.UseCollation(collation);
        }

        builder.HasIndex(member => new { member.TenantId, member.NormalizedIdentifier })
            .IsUnique()
            .HasDatabaseName(Member.UniqueConstraints.IdentifierPerTenant)
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
