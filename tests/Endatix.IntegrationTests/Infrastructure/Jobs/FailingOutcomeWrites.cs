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

    public Task<bool> TryCompleteAsync(long jobId, int claimedAttempt, DateTime utcNow, CancellationToken cancellationToken = default) =>
        failures.TryConsume()
            ? throw new TimeoutException("The database did not answer.")
            : inner.TryCompleteAsync(jobId, claimedAttempt, utcNow, cancellationToken);

    public Task<ClaimedJob?> TryClaimAsync(
        long jobId, IReadOnlyCollection<string> registeredJobTypes, DateTime utcNow, bool recovering = false, CancellationToken cancellationToken = default) =>
        inner.TryClaimAsync(jobId, registeredJobTypes, utcNow, recovering, cancellationToken);

    public Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadStatusAsync(jobId, cancellationToken);

    public Task<JobAttemptState?> ReadAttemptAsync(long jobId, CancellationToken cancellationToken = default) =>
        inner.ReadAttemptAsync(jobId, cancellationToken);

    public Task<bool> TryMirrorNextAttemptAsync(long jobId, DateTime nextAttemptAt, CancellationToken cancellationToken = default) =>
        inner.TryMirrorNextAttemptAsync(jobId, nextAttemptAt, cancellationToken);

    public Task<bool> TryFailAsync(
        long jobId, int claimedAttempt, string errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) =>
        inner.TryFailAsync(jobId, claimedAttempt, errorMessage, utcNow, cancellationToken);

    public Task<bool> TryDeadLetterSpentAsync(
        long jobId, int maxAttempts, string errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) =>
        inner.TryDeadLetterSpentAsync(jobId, maxAttempts, errorMessage, utcNow, cancellationToken);

    public Task<bool> RecordFailedAttemptAsync(
        long jobId,
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        inner.RecordFailedAttemptAsync(jobId, claimedAttempt, maxAttempts, nextAttemptAt, errorMessage, utcNow, cancellationToken);
}
