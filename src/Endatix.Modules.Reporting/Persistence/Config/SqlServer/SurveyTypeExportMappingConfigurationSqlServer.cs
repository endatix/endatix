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
            .HasFilter("[SurveyTypeId] IS NOT NULL");

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[SurveyTypeId] IS NULL");
    }
}
