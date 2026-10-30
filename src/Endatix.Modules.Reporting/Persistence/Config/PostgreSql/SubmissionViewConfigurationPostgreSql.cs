using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<ReportingPostgreSqlDbContext>]
internal sealed class SubmissionViewConfigurationPostgreSql : IEntityTypeConfiguration<SubmissionView>
{
    public void Configure(EntityTypeBuilder<SubmissionView> builder)
    {
        builder.Property(x => x.SettingsJson)
            .HasColumnType("jsonb");
    }
}
