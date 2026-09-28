using System.Collections.Concurrent;
using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

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

    /// <summary>Returns a failure result: a deterministic failure.</summary>
    public const string Fail = "fail";

    /// <summary>Throws an exception whose message looks like a connection string.</summary>
    public const string Throw = "throw";

    /// <summary>Throws on the first attempt and succeeds on the next.</summary>
    public const string ThrowOnce = "throw-once";

    /// <summary>Waits on its token for ever.</summary>
    public const string Block = "block";

    /// <summary>Waits on its token for ever on the first attempt, and succeeds on the next.</summary>
    public const string BlockFirst = "block-first";

    public const string FailureMessage = "Form 12 has no schema.";

    public const string SecretMessage = "Host=db;Password=secret";
}

/// <summary>
/// A handler whose invocations a test can observe, with its behaviour chosen per job by the payload.
/// </summary>
internal sealed class ProbeJobHandler(ProbeInvocations invocations, ILogger<ProbeJobHandler> logger)
    : BackgroundJobHandler<ProbePayload>(logger)
{
    protected override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        ProbePayload payload,
        CancellationToken cancellationToken)
    {
        invocations.Record(job);

        switch (payload.Behaviour)
        {
            case ProbeBehaviours.Fail:
                return Result.Invalid(new ValidationError(ProbeBehaviours.FailureMessage));
            case ProbeBehaviours.Throw:
            case ProbeBehaviours.ThrowOnce when job.AttemptCount == 1:
                throw new InvalidOperationException(ProbeBehaviours.SecretMessage);
            case ProbeBehaviours.Block:
            case ProbeBehaviours.BlockFirst when job.AttemptCount == 1:
                await BlockAsync(job, cancellationToken);
                return Result.Success();
            default:
                return Result.Success();
        }
    }

    private async Task BlockAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            invocations.RecordCancellation(job.JobId);
            throw;
        }
    }
}

/// <summary>What a <see cref="ProbeJobHandler"/> saw, per test.</summary>
internal sealed class ProbeInvocations
{
    private readonly ConcurrentQueue<ProbeRun> _runs = new();
    private readonly ConcurrentDictionary<long, DateTime> _cancellations = new();

    public IReadOnlyCollection<ProbeRun> Runs => _runs;

    public void Record(BackgroundJobContext job) =>
        _runs.Enqueue(new ProbeRun(job, Activity.Current?.TraceId.ToString(), Activity.Current?.Source.Name));

    public void RecordCancellation(long jobId) => _cancellations.TryAdd(jobId, DateTime.UtcNow);

    public DateTime? CancelledAt(long jobId) => _cancellations.TryGetValue(jobId, out var at) ? at : null;

    public int CountFor(long jobId) => _runs.Count(run => run.Job.JobId == jobId);
}

internal sealed record ProbeRun(BackgroundJobContext Job, string? TraceId, string? ActivitySource);
