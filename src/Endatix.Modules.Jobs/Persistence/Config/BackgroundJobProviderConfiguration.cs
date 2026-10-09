using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config;

/// <summary>
/// The provider-dependent half of the <see cref="BackgroundJob"/> mapping: JSON storage, the tenant check
/// constraint and the dedup index.
/// </summary>
/// <remarks>
/// The shape is the same on every provider; only the JSON column type and the identifier quoting in the SQL
/// fragments differ, so a derived configuration supplies those two and nothing else. A change made here
/// therefore reaches every provider at once.
/// </remarks>
internal abstract class BackgroundJobProviderConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    /// <summary>The unique index behind idempotent enqueue; the queue recognises a collision by this name.</summary>
    public const string DedupKeyIndexName = "IX_BackgroundJobs_DedupKey";

    /// <summary>The provider's native JSON column type.</summary>
    protected abstract string JsonColumnType { get; }

    /// <summary>Quotes a column name for a check constraint or an index filter.</summary>
    protected abstract string QuoteIdentifier(string name);

    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType(JsonColumnType);

        // The entity refuses a job without a tenant, but the entity is not the only way a row can
        // arrive: a migration, a seeder or a hand-written statement all bypass it. A row with tenant
        // zero would be hidden from every tenant and visible to every background service, so the
        // rule belongs to the data as well as the code.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_BackgroundJobs_TenantId", $"{QuoteIdentifier(nameof(BackgroundJob.TenantId))} > 0"));

        HasDedupKeyIndex(builder);
    }

    // The guarantee behind idempotent enqueue: a redelivered fan-out cannot insert a second row for the same unit
    // of work, however the two enqueues interleave. Filtered, so callers without a key pay nothing.
    private void HasDedupKeyIndex(EntityTypeBuilder<BackgroundJob> builder) =>
        builder.HasIndex(job => new { job.TenantId, job.JobType, job.DedupKey })
            .IsUnique()
            .HasDatabaseName(DedupKeyIndexName)
            .HasFilter($"{QuoteIdentifier(nameof(BackgroundJob.DedupKey))} IS NOT NULL");
}
