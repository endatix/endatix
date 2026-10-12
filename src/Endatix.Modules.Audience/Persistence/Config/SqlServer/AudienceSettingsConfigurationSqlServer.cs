using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Audience.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Audience.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server mapping for <see cref="AudienceSettings"/>: one active row per tenant.
/// </summary>
[ApplyConfigurationFor<AudienceSqlServerDbContext>]
internal sealed class AudienceSettingsConfigurationSqlServer : IEntityTypeConfiguration<AudienceSettings>
{
    public void Configure(EntityTypeBuilder<AudienceSettings> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Settings_TenantId",
            $"[{nameof(AudienceSettings.TenantId)}] > 0"));

        builder.HasIndex(settings => settings.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_Settings_Tenant")
            .HasFilter($"[{nameof(AudienceSettings.IsDeleted)}] = 0");
    }
}
