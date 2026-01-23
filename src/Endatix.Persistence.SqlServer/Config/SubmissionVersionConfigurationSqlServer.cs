using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.SqlServer.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class SubmissionVersionConfigurationSqlServer : IEntityTypeConfiguration<SubmissionVersion>
    {
        public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
        {
            // Configure JsonData as JSON for SQL Server
            builder.Property(sv => sv.JsonData)
                .HasColumnType("json");
        }
    }
}
