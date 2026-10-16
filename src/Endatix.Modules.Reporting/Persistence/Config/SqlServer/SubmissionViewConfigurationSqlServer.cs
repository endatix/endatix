using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Persistence.Config.SqlServer;

[ApplyConfigurationFor<ReportingSqlServerDbContext>]
internal sealed class SubmissionViewConfigurationSqlServer : IEntityTypeConfiguration<SubmissionView>
{
    public void Configure(EntityTypeBuilder<SubmissionView> builder)
    {
        builder.Property(x => x.SettingsJson)
            .HasColumnType("json");
    }
}
