using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>How many completion writes and claims still fail, shared by every scope of a test node.</summary>
internal sealed class StateRepositoryFailures(int completions = 0, int claims = 0)
{
    private int _completions = completions;
    private int _claims = claims;

    public bool TryConsumeCompletion() => Interlocked.Decrement(ref _completions) >= 0;

    public bool TryConsumeClaim() => Interlocked.Decrement(ref _claims) >= 0;
}

/// <summary>
/// The real state repository, except that its completion writes and claims throw while
/// <see cref="StateRepositoryFailures"/> has failures of that kind left, as they would while the database is
/// unreachable.
/// </summary>
internal sealed class FailingStateRepository(IBackgroundJobStateRepository inner, StateRepositoryFailures failures)
    : IBackgroundJobStateRepository
{
    public static void Register(IServiceCollection services, StateRepositoryFailures failures)
    {
        services.AddSingleton(failures);
        services.AddScoped<IBackgroundJobStateRepository>(provider => new FailingStateRepository(
            new BackgroundJobStateRepository(provider.GetRequiredService<IJobsDbContext>()),
            provider.GetRequiredService<StateRepositoryFailures>()));
    }

    public Task<bool> TryCompleteAsync(AttemptRef attempt, JobFinish finish, CancellationToken cancellationToken = default) =>
        failures.TryConsumeCompletion()
            ? throw new TimeoutException("The database did not answer.")
            : inner.TryCompleteAsync(attempt, finish, cancellationToken);

    public Task<ClaimedJob?> TryClaimAsync(JobClaim claim, CancellationToken cancellationToken = default) =>
        failures.TryConsumeClaim()
            ? throw new TimeoutException("The database did not answer.")
            : inner.TryClaimAsync(claim, cancellationToken);

    public Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadStatusAsync(jobId, cancellationToken);

    public Task<JobAttemptState?> ReadAttemptAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadAttemptAsync(jobId, cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        inner.DeleteExpiredAsync(utcNow, batchSize, cancellationToken);

    public Task<bool> TryFailAsync(AttemptRef attempt, AttemptFailure failure, CancellationToken cancellationToken = default) =>
        inner.TryFailAsync(attempt, failure, cancellationToken);

    public Task<bool> TryDeadLetterSpentAsync(
        AttemptRef lastAttempt, AttemptFailure failure, CancellationToken cancellationToken = default) =>
        inner.TryDeadLetterSpentAsync(lastAttempt, failure, cancellationToken);

    public Task<bool> RecordFailedAttemptAsync(
        AttemptRef attempt, RetryableFailure failure, CancellationToken cancellationToken = default) =>
        inner.RecordFailedAttemptAsync(attempt, failure, cancellationToken);
}
