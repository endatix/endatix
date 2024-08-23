using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Persistence.SqlServer.Config
{
    public class FormConfigurationSqlServer : IEntityTypeConfiguration<Form> 
    {
        public void Configure(EntityTypeBuilder<Form> builder)
        {
            if(!EndatixConfig.Configuration.UseSnowflakeIds)
            {
                builder.Property(s => s.Id)
                    .UseIdentityColumn();
            }
        }
    }
}