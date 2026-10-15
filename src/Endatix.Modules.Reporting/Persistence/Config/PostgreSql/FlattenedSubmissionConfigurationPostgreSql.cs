using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingPostgreSqlDbContext>]
internal sealed class FlattenedSubmissionConfigurationPostgreSql : IEntityTypeConfiguration<FlattenedSubmission>
{
    public void Configure(EntityTypeBuilder<FlattenedSubmission> builder)
    {
        builder.Property(x => x.DataJson)
            .HasColumnType("jsonb");

        // SPIKE: the capped search columns exist only in POC runs (added by the test with ALTER TABLE).
        if (Environment.GetEnvironmentVariable("SPIKE_SEARCH") == "1")
        {
            builder.Property(x => x.SearchJson64).HasColumnType("jsonb");
            builder.Property(x => x.SearchJson128).HasColumnType("jsonb");
        }
        else
        {
            builder.Ignore(x => x.SearchJson64);
            builder.Ignore(x => x.SearchJson128);
        }
    }
}
