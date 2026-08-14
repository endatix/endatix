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

        // The sweeper's eligibility scan (C2) — the hottest query in the system, run on every
        // instance every tick. Filtered, because only Pending and Retrying rows are ever eligible and
        // the terminal rows will vastly outnumber them once webhook delivery is a job type.
        builder.HasIndex(job => new { job.NextAttemptAt, job.Id })
            .HasDatabaseName("IX_BackgroundJobs_Eligible")
            .HasFilter(
                $"[{nameof(BackgroundJob.Status)}] IN ({(int)JobStatus.Pending}, {(int)JobStatus.Retrying})");

        // Stale-worker reaping (C1): only in-flight rows can go stale.
        builder.HasIndex(job => job.HeartbeatAt)
            .HasDatabaseName("IX_BackgroundJobs_Stale")
            .HasFilter($"[{nameof(BackgroundJob.Status)}] = {(int)JobStatus.Processing}");
    }
}
