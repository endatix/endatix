using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config;

internal sealed class FormSchemaConfiguration : IEntityTypeConfiguration<FormSchema>
{
    public void Configure(EntityTypeBuilder<FormSchema> builder)
    {
        builder.ToTable("FormSchemas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.Property(x => x.FormDefinitionRevision).IsRequired();
        builder.Property(x => x.FlatteningMap)
            .IsRequired();
        builder.Property(x => x.Codebook)
            .IsRequired();
        builder.Property(x => x.Locales)
            .IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.FormId }).IsUnique();
    }
}
