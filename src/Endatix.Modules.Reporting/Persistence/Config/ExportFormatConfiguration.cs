using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class ExportFormatConfiguration : IEntityTypeConfiguration<ExportFormat>
{
    public void Configure(EntityTypeBuilder<ExportFormat> builder)
    {
        builder.ToTable("ExportFormats");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(ExportFormat.NAME_MAX_LENGTH);
        
        builder.Property(x => x.ExportTarget)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ExportFormat.EXPORT_TARGET_MAX_LENGTH);
        builder.Property(x => x.DeliveryFormat)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ExportFormat.DELIVERY_FORMAT_MAX_LENGTH);
        builder.Property(x => x.Profile)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(ExportFormat.PROFILE_MAX_LENGTH);
        builder.Property(x => x.Description).HasMaxLength(ExportFormat.DESCRIPTION_MAX_LENGTH);
        builder.Property(x => x.SettingsJson);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
