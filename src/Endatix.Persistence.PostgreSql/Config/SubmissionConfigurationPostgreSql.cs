using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Persistence.PostgreSql.Config
{
    [ApplyConfigurationFor<AppDbContext>]
    public class SubmissionConfigurationPostgreSql : IEntityTypeConfiguration<Submission>
    {
        private const string JSONB_COLUMN_TYPE = "jsonb";

        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            ConfigureJsonColumns(builder);

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

        private static void ConfigureJsonColumns(EntityTypeBuilder<Submission> builder)
        {
            builder.Property(s => s.JsonData)
                .HasColumnType(JSONB_COLUMN_TYPE);
            builder.Property(s => s.Metadata)
                .HasColumnType(JSONB_COLUMN_TYPE);
            builder.Property(s => s.SubmitterProfileSnapshot)
                .HasColumnType(JSONB_COLUMN_TYPE);
            builder.Property(s => s.PersonalizationSnapshot)
                .HasColumnType(JSONB_COLUMN_TYPE);
        }
    }
}
