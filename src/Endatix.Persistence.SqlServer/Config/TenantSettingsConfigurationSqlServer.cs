using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.SqlServer.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class TenantSettingsConfigurationSqlServer : IEntityTypeConfiguration<TenantSettings>
    {
        public void Configure(EntityTypeBuilder<TenantSettings> builder)
        {
            // Configure JSON columns as JSON for SQL Server
            builder.Property(ts => ts.SlackSettingsJson)
                .HasColumnType("json");
            builder.Property(ts => ts.WebHookSettingsJson)
                .HasColumnType("json");
            builder.Property(ts => ts.CustomExportsJson)
                .HasColumnType("json");
        }
    }
}
