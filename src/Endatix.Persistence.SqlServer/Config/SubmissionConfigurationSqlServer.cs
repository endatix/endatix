using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.SqlServer.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class SubmissionConfigurationSqlServer : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            // Configure JSON columns as JSON for SQL Server
            builder.Property(s => s.JsonData)
                .HasColumnType("json");
            builder.Property(s => s.Metadata)
                .HasColumnType("json");

        }
    }
}