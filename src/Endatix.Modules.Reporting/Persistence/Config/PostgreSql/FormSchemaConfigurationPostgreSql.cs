using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FormSchemaConfigurationPostgreSql : IEntityTypeConfiguration<FormSchema>
{
    public void Configure(EntityTypeBuilder<FormSchema> builder)
    {
        builder.Property(x => x.FlatteningMap)
            .HasColumnType("jsonb");
        builder.Property(x => x.Codebook)
            .HasColumnType("jsonb");
        builder.Property(x => x.Locales)
            .HasColumnType("jsonb");
    }
}
