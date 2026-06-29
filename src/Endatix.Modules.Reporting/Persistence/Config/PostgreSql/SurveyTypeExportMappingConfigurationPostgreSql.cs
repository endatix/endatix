using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class SurveyTypeExportMappingConfigurationPostgreSql : IEntityTypeConfiguration<SurveyTypeExportMapping>
{
    public void Configure(EntityTypeBuilder<SurveyTypeExportMapping> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.SurveyTypeId })
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"SurveyTypeId\" IS NOT NULL");

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"SurveyTypeId\" IS NULL");
    }
}
