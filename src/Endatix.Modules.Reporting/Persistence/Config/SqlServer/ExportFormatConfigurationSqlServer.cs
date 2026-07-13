using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class ExportFormatConfigurationSqlServer : IEntityTypeConfiguration<ExportFormat>
{
    public void Configure(EntityTypeBuilder<ExportFormat> builder)
    {
        builder.Property(x => x.SettingsJson)
            .HasColumnType("json");
    }
}
