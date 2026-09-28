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

    public static async Task<JobRow> WaitForStatusAsync(
        this JobsTestDatabase database,
        long jobId,
        Func<JobStatus, bool> reached,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        JobRow? row = null;
        await JobsTestWait.UntilAsync(
            async () =>
            {
                row = await database.ReadJobAsync(jobId, cancellationToken);
                return row is not null && reached(row.Status);
            },
            timeout,
            cancellationToken);
        return row ?? throw new InvalidOperationException($"Job {jobId} has no row.");
    }

    public static Task<long> TriggerCountAsync(
        this JobsTestDatabase database,
        long jobId,
        CancellationToken cancellationToken) =>
        database.CountAsync($"SELECT count(*) FROM jobs.qrtz_triggers WHERE trigger_name = '{jobId}'", cancellationToken);
}
