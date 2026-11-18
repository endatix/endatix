using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

[ApplyConfigurationFor<AppDbContext>()]
public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        // TenantId is the primary key (1-to-1 with Tenant)
        builder.HasKey(branding => branding.TenantId);

        builder.Property(branding => branding.TenantId)
            .ValueGeneratedNever();

        builder.Property(branding => branding.PublishedJson)
            .IsRequired(false);

        builder.Property(branding => branding.PreviousJson)
            .IsRequired(false);

        builder.Property(branding => branding.DraftJson)
            .IsRequired(false);

        builder.Property(branding => branding.Revision)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(branding => branding.PublishedAt)
            .IsRequired(false);

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantBranding>(branding => branding.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
