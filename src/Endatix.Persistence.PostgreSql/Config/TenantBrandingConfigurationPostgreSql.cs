using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>Stores <see cref="TenantBranding"/>'s overrides as <c>jsonb</c> on PostgreSQL.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class TenantBrandingConfigurationPostgreSql : IEntityTypeConfiguration<TenantBranding>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        // jsonb reorders keys: compare stored overrides in canonical form (BrandOverride.IsSameAs).
        builder.Property(branding => branding.PublishedJson)
            .HasColumnType("jsonb");

        builder.Property(branding => branding.PreviousJson)
            .HasColumnType("jsonb");

        builder.Property(branding => branding.DraftJson)
            .HasColumnType("jsonb");
    }
}
