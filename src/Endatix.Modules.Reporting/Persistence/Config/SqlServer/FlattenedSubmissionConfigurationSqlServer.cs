using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

[ApplyConfigurationFor<ReportingSqlServerDbContext>]
internal sealed class FlattenedSubmissionConfigurationSqlServer : IEntityTypeConfiguration<FlattenedSubmission>
{
    public void Configure(EntityTypeBuilder<FlattenedSubmission> builder)
    {
        builder.Property(x => x.DataJson)
            .HasColumnType("json");

        // SPIKE: the capped search columns exist only in POC runs (added by the test with ALTER TABLE).
        if (Environment.GetEnvironmentVariable("SPIKE_SEARCH") == "1")
        {
            builder.Property(x => x.SearchJson64).HasColumnType("json");
            builder.Property(x => x.SearchJson128).HasColumnType("json");
        }
        else
        {
            builder.Ignore(x => x.SearchJson64);
            builder.Ignore(x => x.SearchJson128);
        }
    }
}
