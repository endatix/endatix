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
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SerializationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
    }
}
