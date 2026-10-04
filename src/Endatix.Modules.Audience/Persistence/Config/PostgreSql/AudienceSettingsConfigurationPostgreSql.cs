using Endatix.Modules.Audience.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudienceSettings"/>: one active row per tenant.
/// </summary>
[ApplyConfigurationFor<AudiencePostgreSqlDbContext>]
internal sealed class AudienceSettingsConfigurationPostgreSql : IEntityTypeConfiguration<AudienceSettings>
{
    public void Configure(EntityTypeBuilder<AudienceSettings> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Settings_TenantId",
            $"\"{nameof(AudienceSettings.TenantId)}\" > 0"));

        builder.HasIndex(settings => settings.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_Settings_Tenant")
            .HasFilter($"\"{nameof(AudienceSettings.IsDeleted)}\" = false");
    }
}
