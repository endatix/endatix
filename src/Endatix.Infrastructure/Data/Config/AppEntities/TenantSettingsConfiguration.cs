using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");

        // TenantId is the primary key (1-to-1 with Tenant)
        builder.HasKey(ts => ts.TenantId);

        builder.Property(ts => ts.TenantId)
            .IsRequired();

        builder.Property(ts => ts.SubmissionTokenExpiryHours)
            .IsRequired(false); // Nullable - null means no expiration

        builder.Property(ts => ts.IsSubmissionTokenValidAfterCompletion)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ts => ts.ModifiedAt)
            .IsRequired(false);

        // Foreign key relationship to Tenant
        builder.HasOne(ts => ts.Tenant)
            .WithOne(t => t.Settings)
            .HasForeignKey<TenantSettings>(ts => ts.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
