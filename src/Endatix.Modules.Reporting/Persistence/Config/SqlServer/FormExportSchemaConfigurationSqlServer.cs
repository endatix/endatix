using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FormExportSchemaConfigurationSqlServer : IEntityTypeConfiguration<FormExportSchema>
{
    public void Configure(EntityTypeBuilder<FormExportSchema> builder)
    {
        builder.Property(x => x.SchemaJson)
            .HasColumnType("json");
    }
}
