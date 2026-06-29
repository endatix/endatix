using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class SurveyTypeExportMappingConfigurationSqlServer : IEntityTypeConfiguration<SurveyTypeExportMapping>
{
    public void Configure(EntityTypeBuilder<SurveyTypeExportMapping> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.SurveyTypeId })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [SurveyTypeId] IS NOT NULL");

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [SurveyTypeId] IS NULL");
    }
}
