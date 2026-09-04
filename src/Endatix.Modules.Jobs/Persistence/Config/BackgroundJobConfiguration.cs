using Endatix.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Modules.Jobs.Persistence.Config;

/// <summary>
/// Provider-agnostic mapping for <see cref="BackgroundJob"/>. The JSON column type and the two hot
/// filtered indexes are provider-specific — see the PostgreSql configuration.
/// </summary>
internal sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    private const int JOB_TYPE_MAX_LENGTH = 128;
    private const int STATUS_MESSAGE_MAX_LENGTH = 512;
    private const int ERROR_MESSAGE_MAX_LENGTH = 2048;
    private const int TRACE_ID_MAX_LENGTH = 128;

    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");

        builder.Property(job => job.JobType)
            .HasMaxLength(JOB_TYPE_MAX_LENGTH)
            .IsRequired();

        builder.Property(job => job.TenantId)
            .IsRequired();

        builder.Property(job => job.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(job => job.PayloadJson)
            .IsRequired();

        builder.Property(job => job.ProgressPercentage)
            .IsRequired();

        builder.Property(job => job.StatusMessage)
            .HasMaxLength(STATUS_MESSAGE_MAX_LENGTH);

        // Truncated rather than unbounded: this holds an exception message for an operator to read,
        // not a stack trace to debug from. The full detail belongs in logs, correlated by TraceId.
        builder.Property(job => job.ErrorMessage)
            .HasMaxLength(ERROR_MESSAGE_MAX_LENGTH);

        builder.Property(job => job.TraceId)
            .HasMaxLength(TRACE_ID_MAX_LENGTH);

        builder.Property(job => job.AttemptCount)
            .IsRequired();

        builder.Property(job => job.NextAttemptAt)
            .IsRequired();

        // Serves retention: finding jobs whose artifacts and rows are collectable. Restricting it to
        // terminal rows needs provider-specific filter syntax, so it stays plain here.
        builder.HasIndex(job => job.ExpiresAt)
            .HasDatabaseName("IX_BackgroundJobs_Expiry");

        // Admin and ops reads, and per-tenant concurrency counting.
        builder.HasIndex(job => new { job.TenantId, job.Status, job.CreatedAt })
            .HasDatabaseName("IX_BackgroundJobs_Tenant");
    }
}
