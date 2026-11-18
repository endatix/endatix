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

        // The JSON columns and PublishedAt are optional by their nullable types.
        builder.Property(branding => branding.Revision)
            .IsConcurrencyToken();

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantBranding>(branding => branding.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
