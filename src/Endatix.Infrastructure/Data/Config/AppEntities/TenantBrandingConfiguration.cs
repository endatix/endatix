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

        // Get-only until draft editing writes it; mapped through its backing field.
        builder.Property(branding => branding.DraftJson);

        builder.Property(branding => branding.Revision)
            .IsConcurrencyToken();

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantBranding>(branding => branding.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
