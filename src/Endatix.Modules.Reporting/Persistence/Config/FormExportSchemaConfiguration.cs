using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Persistence.Config;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FormExportSchemaConfiguration : IEntityTypeConfiguration<FormExportSchema>
{
    public void Configure(EntityTypeBuilder<FormExportSchema> builder)
    {
        builder.ToTable("FormExportSchemas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.Property(x => x.FormDefinitionRevision).IsRequired();
        builder.Property(x => x.SchemaJson)
            .IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.FormId }).IsUnique();
    }
}
