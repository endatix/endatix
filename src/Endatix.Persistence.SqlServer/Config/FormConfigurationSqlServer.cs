using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.SqlServer.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class FormConfigurationSqlServer : IEntityTypeConfiguration<Form>
    {
        public void Configure(EntityTypeBuilder<Form> builder)
        {
            // Configure WebHookSettingsJson as JSON for SQL Server
            builder.Property(f => f.WebHookSettingsJson)
                .HasColumnType("json");
        }
    }
}