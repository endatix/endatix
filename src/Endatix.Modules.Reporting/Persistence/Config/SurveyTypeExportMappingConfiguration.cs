using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Persistence.Config;

internal sealed class SurveyTypeExportMappingConfiguration : IEntityTypeConfiguration<SurveyTypeExportMapping>
{
    public void Configure(EntityTypeBuilder<SurveyTypeExportMapping> builder)
    {
        builder.ToTable("SurveyTypeExportMappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(x => x.ExportFormat)
            .WithMany()
            .HasForeignKey(x => x.ExportFormatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
