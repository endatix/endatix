using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.SqlServer.Config
{
    public class CustomQuestionConfigurationSqlServer : IEntityTypeConfiguration<CustomQuestion> 
    {
        public void Configure(EntityTypeBuilder<CustomQuestion> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(q => q.Id)
                    .UseIdentityColumn();
            }
            
            // Configure JsonData as NVARCHAR(MAX) for SQL Server
            builder.Property(q => q.JsonData)
                .HasColumnType("nvarchar(max)");
        }
    }
} 