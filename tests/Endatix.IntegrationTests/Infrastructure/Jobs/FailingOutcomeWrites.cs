using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>How many completion writes still fail, shared by every scope of a test node.</summary>
internal sealed class OutcomeWriteFailures(int count)
{
    private int _remaining = count;

    public bool TryConsume() => Interlocked.Decrement(ref _remaining) >= 0;
}

/// <summary>
/// The real state repository, except that its completion writes throw while <see cref="OutcomeWriteFailures"/>
/// has failures left, as they would while the database is unreachable.
/// </summary>
internal sealed class FailingOutcomeWrites(IBackgroundJobStateRepository inner, OutcomeWriteFailures failures)
    : IBackgroundJobStateRepository
{
    public static void Register(IServiceCollection services, OutcomeWriteFailures failures)
    {
        services.AddSingleton(failures);
        services.AddScoped<IBackgroundJobStateRepository>(provider => new FailingOutcomeWrites(
            new BackgroundJobStateRepository(provider.GetRequiredService<IJobsDbContext>()),
            provider.GetRequiredService<OutcomeWriteFailures>()));
    }

    public Task<bool> TryCompleteAsync(AttemptRef attempt, JobFinish finish, CancellationToken cancellationToken = default) =>
        failures.TryConsume()
            ? throw new TimeoutException("The database did not answer.")
            : inner.TryCompleteAsync(attempt, finish, cancellationToken);

    public Task<ClaimedJob?> TryClaimAsync(JobClaim claim, CancellationToken cancellationToken = default) =>
        inner.TryClaimAsync(claim, cancellationToken);

    public Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadStatusAsync(jobId, cancellationToken);

    public Task<JobAttemptState?> ReadAttemptAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadAttemptAsync(jobId, cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        inner.DeleteExpiredAsync(utcNow, batchSize, cancellationToken);

    public Task<bool> TryMirrorNextAttemptAsync(long jobId, DateTime nextAttemptAt, CancellationToken cancellationToken = default) =>
        inner.TryMirrorNextAttemptAsync(jobId, nextAttemptAt, cancellationToken);

    public Task<bool> TryFailAsync(AttemptRef attempt, AttemptFailure failure, CancellationToken cancellationToken = default) =>
        inner.TryFailAsync(attempt, failure, cancellationToken);

    public Task<bool> TryDeadLetterSpentAsync(
        AttemptRef lastAttempt, AttemptFailure failure, CancellationToken cancellationToken = default) =>
        inner.TryDeadLetterSpentAsync(lastAttempt, failure, cancellationToken);

    public Task<bool> RecordFailedAttemptAsync(
        AttemptRef attempt, RetryableFailure failure, CancellationToken cancellationToken = default) =>
        inner.RecordFailedAttemptAsync(attempt, failure, cancellationToken);
}
