using Endatix.Modules.Personalization.Persistence;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Personalization.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Personalization.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL mapping for <see cref="AudienceSettings"/>: one active row per tenant.
/// </summary>
[ApplyConfigurationFor<PersonalizationPostgreSqlDbContext>]
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
