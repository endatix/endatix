using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingDbContext>]
internal sealed class FlattenedSubmissionConfigurationPostgreSql : IEntityTypeConfiguration<FlattenedSubmission>
{
    public void Configure(EntityTypeBuilder<FlattenedSubmission> builder)
    {
        builder.Property(x => x.DataJson)
            .HasColumnType("jsonb");
    }
}
