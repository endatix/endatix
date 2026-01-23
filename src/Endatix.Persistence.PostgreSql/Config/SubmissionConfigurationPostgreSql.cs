using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class SubmissionConfigurationPostgreSql : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            // Configure JsonData as JSONB for PostgreSQL
            builder.Property(s => s.JsonData)
                .HasColumnType("jsonb");

            // Configure Metadata as JSONB for PostgreSQL
            builder.Property(s => s.Metadata)
                .HasColumnType("jsonb");
        }
    }
}
