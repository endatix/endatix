using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.PostgreSql.Config
{
    public class CustomQuestionConfigurationPostgreSql : IEntityTypeConfiguration<CustomQuestion> 
    {
        public void Configure(EntityTypeBuilder<CustomQuestion> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(q => q.Id)
                    .UseIdentityColumn();
            }
            
            // Configure JsonData as JSONB for better query performance
            builder.Property(q => q.JsonData)
                .HasColumnType("jsonb");
        }
    }
} 