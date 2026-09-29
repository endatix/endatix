using Endatix.Infrastructure.Data.Config;
using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config.PostgreSql;

/// <summary>
/// The provider-dependent half of the <see cref="BackgroundJob"/> mapping: JSON storage and the tenant
/// check constraint.
/// </summary>
/// <remarks>
/// Only the JSON column type and the identifier quoting are genuinely provider-specific. When a second
/// provider is added, lift the shared shape into a base class and leave only those two things to the
/// derived types — otherwise a change can be made here and forgotten there.
/// </remarks>
[ApplyConfigurationFor<JobsPostgreSqlDbContext>]
internal sealed class BackgroundJobConfigurationPostgreSql : IEntityTypeConfiguration<BackgroundJob>
{
    /// <summary>The unique index behind idempotent enqueue; the queue recognises a collision by this name.</summary>
    public const string DedupKeyIndexName = "IX_BackgroundJobs_DedupKey";

    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.Property(job => job.PayloadJson)
            .HasColumnType("jsonb");

        var tenantId = $"\"{nameof(BackgroundJob.TenantId)}\"";

        // The entity refuses a job without a tenant, but the entity is not the only way a row can
        // arrive: a migration, a seeder or a hand-written statement all bypass it. A row with tenant
        // zero would be hidden from every tenant and visible to every background service, so the
        // rule belongs to the data as well as the code.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_BackgroundJobs_TenantId", $"{tenantId} > 0"));

        HasDedupKeyIndex(builder);
    }

    // The guarantee behind idempotent enqueue: a redelivered fan-out cannot insert a second row for the same unit
    // of work, however the two enqueues interleave. Filtered, so callers without a key pay nothing.
    private static void HasDedupKeyIndex(EntityTypeBuilder<BackgroundJob> builder) =>
        builder.HasIndex(job => new { job.TenantId, job.JobType, job.DedupKey })
            .IsUnique()
            .HasDatabaseName(DedupKeyIndexName)
            .HasFilter($"\"{nameof(BackgroundJob.DedupKey)}\" IS NOT NULL");
}
