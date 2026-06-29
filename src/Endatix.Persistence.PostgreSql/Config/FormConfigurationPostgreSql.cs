using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class FormConfigurationPostgreSql : IEntityTypeConfiguration<Form>
    {
        public void Configure(EntityTypeBuilder<Form> builder)
        {
            // Configure WebHookSettingsJson as JSONB for PostgreSQL
            builder.Property(f => f.WebHookSettingsJson)
                .HasColumnType("jsonb");

            // Configure Metadata as JSONB for PostgreSQL
            builder.Property(f => f.Metadata)
                .HasColumnType("jsonb");
        }
    }
}