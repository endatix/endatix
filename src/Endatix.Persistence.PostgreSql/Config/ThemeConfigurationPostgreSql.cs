using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.PostgreSql.Config
{
    public class ThemeConfigurationPostgreSql : IEntityTypeConfiguration<Theme> 
    {
        public void Configure(EntityTypeBuilder<Theme> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(t => t.Id)
                    .UseIdentityColumn();
            }
            
            // Configure JsonData as JSONB for better query performance
            builder.Property(t => t.JsonData)
                .HasColumnType("jsonb");
        }
    }
} 