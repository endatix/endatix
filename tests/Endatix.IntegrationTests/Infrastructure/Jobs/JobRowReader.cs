using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

internal sealed record JobRow(
    long Id,
    JobStatus Status,
    int AttemptCount,
    int ProgressPercentage,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime NextAttemptAt,
    DateTime? ModifiedAt,
    DateTime? CompletedAt,
    DateTime? ExpiresAt,
    string JobType,
    long TenantId);

/// <summary>A status a job row is waited for, and how long to wait before giving up.</summary>
internal sealed record ExpectedJobStatus(long JobId, JobStatus Status, TimeSpan Timeout);

internal static class JobRowReader
{
    public static async Task<JobRow?> ReadJobAsync(
        this JobsTestDatabase database,
        long jobId,
        CancellationToken cancellationToken) =>
        (await database.QueryAsync(
            $"""
             SELECT "Id", "Status", "AttemptCount", "ProgressPercentage", "ErrorMessage", "StartedAt", "NextAttemptAt",
                    "ModifiedAt", "CompletedAt", "ExpiresAt", "JobType", "TenantId"
             FROM jobs."BackgroundJobs" WHERE "Id" = {jobId}
             """,
            reader => new JobRow(
                reader.GetInt64(0),
                (JobStatus)reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                reader.GetDateTime(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.GetString(10),
                reader.GetInt64(11)),
            cancellationToken)).SingleOrDefault();

    /// <summary>How long a wait for a row's status lasts unless the caller names a timeout.</summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromSeconds(30);

    public static Task<JobRow> WaitForStatusAsync(
        this JobsTestDatabase database,
        long jobId,
        JobStatus status,
        CancellationToken cancellationToken) =>
        database.WaitForStatusAsync(new ExpectedJobStatus(jobId, status, DefaultPatience), cancellationToken);

    public static async Task<JobRow> WaitForStatusAsync(
        this JobsTestDatabase database,
        ExpectedJobStatus expected,
        CancellationToken cancellationToken)
    {
        JobRow? row = null;
        await JobsTestWait.UntilAsync(
            async () =>
            {
                row = await database.ReadJobAsync(expected.JobId, cancellationToken);
                return row is not null && row.Status == expected.Status;
            },
            expected.Timeout,
            cancellationToken);
        return row ?? throw new InvalidOperationException($"Job {expected.JobId} has no row.");
    }

    public static Task<long> TriggerCountAsync(
        this JobsTestDatabase database,
        long jobId,
        CancellationToken cancellationToken) =>
        database.CountAsync($"SELECT count(*) FROM jobs.qrtz_triggers WHERE trigger_name = '{jobId}'", cancellationToken);
}
