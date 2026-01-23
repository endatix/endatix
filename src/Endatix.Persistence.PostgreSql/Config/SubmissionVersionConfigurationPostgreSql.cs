using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class SubmissionVersionConfigurationPostgreSql : IEntityTypeConfiguration<SubmissionVersion>
    {
        public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
        {
            // Configure JsonData as JSONB for PostgreSQL
            builder.Property(sv => sv.JsonData)
                .HasColumnType("jsonb");
        }
    }
}
