using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

/// <summary>The core JSON column types, as the core <c>SubmissionConfigurationPostgreSql</c> maps them.</summary>
[ApplyConfigurationFor<ReportingPostgreSqlDbContext>]
internal sealed class CoreSubmissionRowConfigurationPostgreSql : IEntityTypeConfiguration<CoreSubmissionRow>
{
    public void Configure(EntityTypeBuilder<CoreSubmissionRow> builder)
    {
        builder.Property(x => x.JsonData)
            .HasColumnType("jsonb");
        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");
        builder.Property(x => x.SubmitterProfileSnapshot)
            .HasColumnType("jsonb");
    }
}
