using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config;

/// <summary>Stores each <see cref="TenantBrandingVersion"/>'s override as <c>jsonb</c> on PostgreSQL. jsonb reorders keys, so the aggregate compares content in canonical form.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class TenantBrandingVersionConfigurationPostgreSql : IEntityTypeConfiguration<TenantBrandingVersion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBrandingVersion> builder)
    {
        builder.Property(version => version.Json)
            .HasColumnType("jsonb");
    }
}
