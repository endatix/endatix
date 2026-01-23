using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class TenantSettingsConfigurationPostgreSql : IEntityTypeConfiguration<TenantSettings>
    {
        public void Configure(EntityTypeBuilder<TenantSettings> builder)
        {
            // Configure all JSON columns as JSONB for PostgreSQL
            builder.Property(ts => ts.SlackSettingsJson)
                .HasColumnType("jsonb");

            builder.Property(ts => ts.WebHookSettingsJson)
                .HasColumnType("jsonb");

            builder.Property(ts => ts.CustomExportsJson)
                .HasColumnType("jsonb");
        }
    }
}
