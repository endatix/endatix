using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.PostgreSql.Config
{
    public class SubmissionConfigurationPostgreSql : IEntityTypeConfiguration<Submission> 
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(s => s.Id)
                    .UseIdentityColumn();
            }
        }
    }
}