using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config.PostgreSql;

/// <summary>
/// The provider-dependent half of the <see cref="BackgroundJob"/> mapping: JSON storage and the two
/// filtered indexes that carry the queue's hot queries.
/// </summary>
/// <remarks>
/// Only the JSON column type and the identifier quoting are genuinely provider-specific. A second
/// provider needs the same indexes over the same columns with the same predicates, so when one is
/// added, lift the shared shape into a base class and leave only those two things to the derived
/// types — otherwise an index change can be made here and forgotten there.
/// </remarks>
[ApplyConfigurationFor<JobsPostgreSqlDbContext>]
internal sealed class BackgroundJobConfigurationPostgreSql : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType("jsonb");

        builder.Property(job => job.ResultJson)
            .HasColumnType("jsonb");

        var status = $"\"{nameof(BackgroundJob.Status)}\"";

        // Serves the sweeper's "which jobs are due?" scan — the hottest query here, run on every
        // instance on every tick. Filtered, because only Pending and Retrying rows are ever eligible,
        // and terminal rows come to outnumber them by orders of magnitude.
        builder.HasIndex(job => new { job.NextAttemptAt, job.Id })
            .HasDatabaseName("IX_BackgroundJobs_Eligible")
            .HasFilter($"{status} IN ({(int)JobStatus.Pending}, {(int)JobStatus.Retrying})");

        // Serves the scan for jobs whose worker died mid-run: only in-flight rows can go stale.
        builder.HasIndex(job => job.HeartbeatAt)
            .HasDatabaseName("IX_BackgroundJobs_Stale")
            .HasFilter($"{status} = {(int)JobStatus.Processing}");
    }
}
