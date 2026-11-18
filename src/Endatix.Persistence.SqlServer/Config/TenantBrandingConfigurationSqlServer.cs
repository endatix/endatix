using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.SqlServer.Config;

[ApplyConfigurationFor<AppDbContext>]
public class TenantBrandingConfigurationSqlServer : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.Property(branding => branding.PublishedJson)
            .HasColumnType("json");

        builder.Property(branding => branding.PreviousJson)
            .HasColumnType("json");

        builder.Property(branding => branding.DraftJson)
            .HasColumnType("json");
    }
}
