using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.SqlServer.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class FormDefinitionConfigurationSqlServer : IEntityTypeConfiguration<FormDefinition>
    {
        public void Configure(EntityTypeBuilder<FormDefinition> builder)
        {
            // Configure JsonData as JSON for SQL Server
            builder.Property(fd => fd.JsonData)
                .HasColumnType("json");
        }
    }
}
