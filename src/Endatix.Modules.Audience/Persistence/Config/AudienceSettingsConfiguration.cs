using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// Mapping for <see cref="AudienceSettings"/>: one active row per tenant.
/// </summary>
internal sealed class AudienceSettingsConfiguration(AudienceSqlDialect dialect)
    : IEntityTypeConfiguration<AudienceSettings>
{
    public const int IdentifierKindMaxLength = 32;

    public void Configure(EntityTypeBuilder<AudienceSettings> builder)
    {
        builder.ToTable("Settings", table => table.HasCheckConstraint(
            "CK_Settings_TenantId", dialect.PositiveTenantIdCheck));

        builder.Property(settings => settings.TenantId).IsRequired();

        builder.Property(settings => settings.IdentifierKind)
            .HasMaxLength(IdentifierKindMaxLength)
            .IsRequired();

        builder.HasIndex(settings => settings.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_Settings_Tenant")
            .HasFilter(dialect.ActiveRowsFilter);
    }
}
