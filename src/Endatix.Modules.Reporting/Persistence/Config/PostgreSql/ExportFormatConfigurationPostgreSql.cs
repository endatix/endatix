using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class ExportFormatConfigurationPostgreSql : IEntityTypeConfiguration<ExportFormat>
{
    public void Configure(EntityTypeBuilder<ExportFormat> builder)
    {
        builder.Property(x => x.SettingsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .HasFilter($"\"{nameof(ExportFormat.IsDeleted)}\" = false");
    }
}
