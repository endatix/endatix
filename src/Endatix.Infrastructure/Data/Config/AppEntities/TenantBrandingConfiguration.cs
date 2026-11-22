using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Data.Config.AppEntities;

/// <summary>Maps <see cref="TenantBranding"/>: the tenant id as key and foreign key, and <c>Version</c> as the concurrency token.</summary>
[ApplyConfigurationFor<AppDbContext>()]
internal sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("TenantBrandings");

        // TenantId is the primary key (1-to-1 with Tenant)
        builder.HasKey(branding => branding.TenantId);

        builder.Property(branding => branding.TenantId)
            .ValueGeneratedNever();

        builder.Property(branding => branding.Version)
            .IsConcurrencyToken();

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantBranding>(branding => branding.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
