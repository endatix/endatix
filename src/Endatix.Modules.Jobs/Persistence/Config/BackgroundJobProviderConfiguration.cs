using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config;

/// <summary>
/// The provider-dependent half of the <see cref="BackgroundJob"/> mapping: JSON storage and the two
/// filtered indexes that carry the queue's hot queries.
/// </summary>
/// <remarks>
/// Both providers need the same indexes over the same columns with the same predicates; only the
/// JSON column type and the identifier-quoting style differ. Keeping the shape here and letting
/// derived classes supply just those two things means a change to an index cannot be applied to one
/// provider and forgotten on the other.
/// </remarks>
internal abstract class BackgroundJobProviderConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    /// <summary>Native JSON column type, e.g. <c>jsonb</c> or <c>json</c>.</summary>
    protected abstract string JsonColumnType { get; }

    /// <summary>Wraps a column name in this provider's identifier quoting, for raw filter SQL.</summary>
    protected abstract string QuoteIdentifier(string columnName);

    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType(JsonColumnType);

        builder.Property(job => job.ResultJson)
            .HasColumnType(JsonColumnType);

        var status = QuoteIdentifier(nameof(BackgroundJob.Status));

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
