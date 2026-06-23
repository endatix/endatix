using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FormExportSchemaConfigurationPostgreSql : IEntityTypeConfiguration<FormExportSchema>
{
    public void Configure(EntityTypeBuilder<FormExportSchema> builder)
    {
        builder.Property(x => x.SchemaJson)
            .HasColumnType("jsonb");
    }
}
