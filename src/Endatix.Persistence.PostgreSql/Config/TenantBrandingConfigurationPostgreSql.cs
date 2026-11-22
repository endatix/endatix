using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>Stores <see cref="TenantBranding"/>'s live override as <c>jsonb</c> on PostgreSQL. jsonb reorders keys, so the aggregate compares content in canonical form.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class TenantBrandingConfigurationPostgreSql : IEntityTypeConfiguration<TenantBranding>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.Property(branding => branding.PublishedJson)
            .HasColumnType("jsonb");
    }
}
