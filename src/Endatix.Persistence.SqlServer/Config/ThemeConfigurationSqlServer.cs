using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.SqlServer.Config
{
    public class ThemeConfigurationSqlServer : IEntityTypeConfiguration<Theme> 
    {
        public void Configure(EntityTypeBuilder<Theme> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(t => t.Id)
                    .UseIdentityColumn();
            }
            
            // Configure JsonData as NVARCHAR(MAX) for SQL Server
            builder.Property(t => t.JsonData)
                .HasColumnType("nvarchar(max)");
        }
    }
} 