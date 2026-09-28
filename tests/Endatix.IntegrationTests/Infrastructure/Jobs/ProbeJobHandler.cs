using System.Collections.Concurrent;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// The payload of <see cref="ProbeJobHandler"/>: which behaviour the handler plays for this job.
/// </summary>
internal sealed record ProbePayload(string Behaviour = ProbeBehaviours.Succeed) : IBackgroundJobPayload
{
    public static string JobType => "IntegrationProbe";
}

internal static class ProbeBehaviours
{
    public const string Succeed = "succeed";
}

/// <summary>
/// A handler whose invocations a test can count, shared by every node in the process.
/// </summary>
internal sealed class ProbeJobHandler(ProbeInvocations invocations) : BackgroundJobHandler<ProbePayload>
{
    protected override Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        ProbePayload payload,
        CancellationToken cancellationToken)
    {
        invocations.Record(job);
        return Task.FromResult(Result.Success());
    }
}

/// <summary>What a <see cref="ProbeJobHandler"/> saw, per test.</summary>
internal sealed class ProbeInvocations
{
    private readonly ConcurrentQueue<BackgroundJobContext> _jobs = new();

    public IReadOnlyCollection<BackgroundJobContext> Jobs => _jobs;

    public void Record(BackgroundJobContext job) => _jobs.Enqueue(job);

    public int CountFor(long jobId) => _jobs.Count(job => job.JobId == jobId);
}
