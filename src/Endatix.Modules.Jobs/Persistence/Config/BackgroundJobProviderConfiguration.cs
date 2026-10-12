using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config;

/// <summary>
/// The provider-dependent half of the <see cref="BackgroundJob"/> mapping: JSON storage, the dedup key's
/// comparison, the tenant check constraint and the dedup index.
/// </summary>
/// <remarks>
/// The shape is the same on every provider; only the JSON column type, the identifier quoting in the SQL
/// fragments and, where the database's default differs from .NET's, the dedup columns' collation differ, so a
/// derived configuration supplies those and nothing else. A change made here therefore reaches every provider
/// at once.
/// </remarks>
internal abstract class BackgroundJobProviderConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    /// <summary>The unique index behind idempotent enqueue; the queue recognises a collision by this name.</summary>
    public const string DedupKeyIndexName = "IX_BackgroundJobs_DedupKey";

    /// <summary>The provider's native JSON column type.</summary>
    protected abstract string JsonColumnType { get; }

    /// <summary>Quotes a column name for a check constraint or an index filter.</summary>
    protected abstract string QuoteIdentifier(string name);

    /// <summary>
    /// A collation that compares the dedup identity's text columns the way the queue's in-memory lookup does,
    /// ordinally, or <see langword="null"/> when the database's default already does.
    /// </summary>
    protected virtual string? OrdinalCollation => null;

    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType(JsonColumnType);

        UseOrdinalComparisonForDedupIdentity(builder);

        // The entity refuses a job without a tenant, but the entity is not the only way a row can
        // arrive: a migration, a seeder or a hand-written statement all bypass it. A row with tenant
        // zero would be hidden from every tenant and visible to every background service, so the
        // rule belongs to the data as well as the code.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_BackgroundJobs_TenantId", $"{QuoteIdentifier(nameof(BackgroundJob.TenantId))} > 0"));

        HasDedupKeyIndex(builder);
    }

    // The queue finds an existing job by matching the stored key and job type ordinally. Were the unique index
    // to compare them case-insensitively, keys differing only in case would miss that lookup and then collide on
    // the index, failing the enqueue instead of returning the existing job.
    private void UseOrdinalComparisonForDedupIdentity(EntityTypeBuilder<BackgroundJob> builder)
    {
        if (OrdinalCollation is not { } collation)
        {
            return;
        }

        builder.Property(job => job.JobType).UseCollation(collation);
        builder.Property(job => job.DedupKey).UseCollation(collation);
    }

    // The guarantee behind idempotent enqueue: a redelivered fan-out cannot insert a second row for the same unit
    // of work, however the two enqueues interleave. Filtered, so callers without a key pay nothing.
    private void HasDedupKeyIndex(EntityTypeBuilder<BackgroundJob> builder) =>
        builder.HasIndex(job => new { job.TenantId, job.JobType, job.DedupKey })
            .IsUnique()
            .HasDatabaseName(DedupKeyIndexName)
            .HasFilter($"{QuoteIdentifier(nameof(BackgroundJob.DedupKey))} IS NOT NULL");
}
