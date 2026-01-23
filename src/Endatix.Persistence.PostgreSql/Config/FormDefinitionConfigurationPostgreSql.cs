using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class FormDefinitionConfigurationPostgreSql : IEntityTypeConfiguration<FormDefinition>
    {
        public void Configure(EntityTypeBuilder<FormDefinition> builder)
        {
            // Configure JsonData as JSONB for PostgreSQL
            builder.Property(fd => fd.JsonData)
                .HasColumnType("jsonb");
        }
    }
}
