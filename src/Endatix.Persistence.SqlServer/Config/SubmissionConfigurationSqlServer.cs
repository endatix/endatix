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
            builder.Property(s => s.SubmitterProfileSnapshot)
                .HasColumnType("json");

            builder.HasIndex(s => s.RestrictionKey)
                .HasDatabaseName("UX_Submissions_RestrictionKey")
                .IsUnique()
                .HasFilter(
                    $"[{nameof(Submission.RestrictionKey)}] IS NOT NULL AND [{nameof(Submission.IsDeleted)}] = 0");

            ConfigureCollectionStatusIndexes(builder);
        }

        private static void ConfigureCollectionStatusIndexes(EntityTypeBuilder<Submission> builder)
        {
            builder.HasIndex(s => new { s.FormId, s.CollectionStatus })
                .HasDatabaseName("IX_Submissions_FormId_CollectionStatus")
                .HasFilter($"[{nameof(Submission.IsDeleted)}] = 0");

            builder.HasIndex(s => s.ModifiedAt)
                .HasDatabaseName("IX_Submissions_InProgress_ModifiedAt")
                .HasFilter("[CollectionStatus] = 'in_progress' AND [IsDeleted] = 0");
        }
    }
}
