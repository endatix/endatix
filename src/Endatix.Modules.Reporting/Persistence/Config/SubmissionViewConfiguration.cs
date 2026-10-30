using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config;

internal sealed class SubmissionViewConfiguration : IEntityTypeConfiguration<SubmissionView>
{
    public void Configure(EntityTypeBuilder<SubmissionView> builder)
    {
        builder.ToTable("SubmissionViews");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.FormId).IsRequired();
        builder.Property(x => x.OwnerUserId)
            .IsRequired()
            .HasDefaultValue(SubmissionView.SharedOwnerUserId);
        builder.Property(x => x.SettingsJson).IsRequired();

        // Not PostgreSQL xmin: the module runs on SQL Server too, where a rowversion is a byte array.
        builder.Property(x => x.Version)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.FormId, x.OwnerUserId }).IsUnique();
    }
}
