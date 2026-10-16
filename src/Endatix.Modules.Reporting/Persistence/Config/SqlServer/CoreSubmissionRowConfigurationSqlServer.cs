using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

/// <summary>The core JSON column types, as the core <c>SubmissionConfigurationSqlServer</c> maps them.</summary>
[ApplyConfigurationFor<ReportingSqlServerDbContext>]
internal sealed class CoreSubmissionRowConfigurationSqlServer : IEntityTypeConfiguration<CoreSubmissionRow>
{
    public void Configure(EntityTypeBuilder<CoreSubmissionRow> builder)
    {
        builder.Property(x => x.JsonData)
            .HasColumnType("json");
        builder.Property(x => x.Metadata)
            .HasColumnType("json");
        builder.Property(x => x.SubmitterProfileSnapshot)
            .HasColumnType("json");
    }
}
