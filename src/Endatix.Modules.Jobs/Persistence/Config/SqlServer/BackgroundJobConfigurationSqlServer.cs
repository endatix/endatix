using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config.SqlServer;

[ApplyConfigurationFor<JobsSqlServerDbContext>]
internal sealed class BackgroundJobConfigurationSqlServer : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType("json");

        builder.Property(job => job.ResultJson)
            .HasColumnType("json");

        // Serves the sweeper's "which jobs are due?" scan — the hottest query here, run on every
        // instance on every tick. Filtered, because only Pending and Retrying rows are ever eligible,
        // and terminal rows come to outnumber them by orders of magnitude.
        builder.HasIndex(job => new { job.NextAttemptAt, job.Id })
            .HasDatabaseName("IX_BackgroundJobs_Eligible")
            .HasFilter(
                $"[{nameof(BackgroundJob.Status)}] IN ({(int)JobStatus.Pending}, {(int)JobStatus.Retrying})");

        // Serves the scan for jobs whose worker died mid-run: only in-flight rows can go stale.
        builder.HasIndex(job => job.HeartbeatAt)
            .HasDatabaseName("IX_BackgroundJobs_Stale")
            .HasFilter($"[{nameof(BackgroundJob.Status)}] = {(int)JobStatus.Processing}");
    }
}
