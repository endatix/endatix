using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Persistence.Config;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class ExportFormatConfiguration : IEntityTypeConfiguration<ExportFormat>
{
    public void Configure(EntityTypeBuilder<ExportFormat> builder)
    {
        builder.ToTable("ExportFormats");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ExportFormat.NameMaxLength);
        builder.Property(x => x.SerializationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ExportFormat.SerializationTypeMaxLength);
        builder.Property(x => x.Description).HasMaxLength(ExportFormat.DescriptionMaxLength);
        builder.Property(x => x.SettingsJson);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
    }
}
