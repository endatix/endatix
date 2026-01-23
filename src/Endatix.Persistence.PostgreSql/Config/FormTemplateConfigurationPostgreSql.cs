using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class FormTemplateConfigurationPostgreSql : IEntityTypeConfiguration<FormTemplate>
    {
        public void Configure(EntityTypeBuilder<FormTemplate> builder)
        {
            // Configure JsonData as JSONB for PostgreSQL
            builder.Property(ft => ft.JsonData)
                .HasColumnType("jsonb");
        }
    }
}
