using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

/// <summary>Stores <see cref="TenantBranding"/>'s live override as <c>json</c> on SQL Server.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class TenantBrandingConfigurationSqlServer : IEntityTypeConfiguration<TenantBranding>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.Property(branding => branding.PublishedJson)
            .HasColumnType("json");
    }
}
