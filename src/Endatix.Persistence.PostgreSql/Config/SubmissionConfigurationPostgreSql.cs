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

            builder.Property(s => s.SubmitterProfileSnapshot)
                .HasColumnType("jsonb");

            builder.HasIndex(s => s.SubmitterProfileSnapshot)
                .HasDatabaseName("IX_Submissions_SubmitterProfileSnapshot_GIN")
                .HasMethod("gin")
                .HasOperators("jsonb_path_ops");

            builder.HasIndex(s => s.RestrictionKey)
                .HasDatabaseName("UX_Submissions_RestrictionKey")
                .IsUnique()
                .HasFilter(
                    $"\"{nameof(Submission.RestrictionKey)}\" IS NOT NULL AND \"{nameof(Submission.IsDeleted)}\" = false");

            builder.HasIndex(s => new { s.FormId, s.CollectionStatus })
                .HasDatabaseName("IX_Submissions_FormId_CollectionStatus")
                .HasFilter($"\"{nameof(Submission.IsDeleted)}\" = false");

            builder.HasIndex(s => s.ModifiedAt)
                .HasDatabaseName("IX_Submissions_InProgress_ModifiedAt")
                .HasFilter("\"CollectionStatus\" = 'in_progress' AND \"IsDeleted\" = false");
        }
    }
}
