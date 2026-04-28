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

            builder.HasIndex(s => new { s.FormId, s.SubmittedBy })
                .HasDatabaseName("UX_Submissions_FormId_SubmittedBy")
                .IsUnique()
                .HasFilter("[IsTestSubmission] = 0 AND [SubmittedBy] IS NOT NULL");
        }
    }
}