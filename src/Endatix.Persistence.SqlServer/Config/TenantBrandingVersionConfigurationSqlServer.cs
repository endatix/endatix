using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

/// <summary>Stores each <see cref="TenantBrandingVersion"/>'s override as <c>json</c> on SQL Server.</summary>
[ApplyConfigurationFor<AppDbContext>]
internal sealed class TenantBrandingVersionConfigurationSqlServer : IEntityTypeConfiguration<TenantBrandingVersion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantBrandingVersion> builder)
    {
        builder.Property(version => version.Json)
            .HasColumnType("json");
    }
}
