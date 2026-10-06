using System.Collections.Concurrent;
using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

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

    /// <summary>Waits on its token for ever on the first attempt, and throws on every later one.</summary>
    public const string BlockFirstThenThrow = "block-first-then-throw";

    /// <summary>
    /// Waits on its token for ever on the first attempt, throws on the second once its node's scheduler has started
    /// shutting down, and succeeds on every later one.
    /// </summary>
    public const string BlockFirstThenThrowWhileStopping = "block-first-then-throw-while-stopping";

    /// <summary>
    /// Waits on its token on the first attempt and returns success once that is cancelled, as a handler that finishes
    /// its work just as its node stops; succeeds on every later one.
    /// </summary>
    public const string FinishFirstAsNodeStops = "finish-first-as-node-stops";

    public const string FailureMessage = "Form 12 has no schema.";

    public const string SecretMessage = "Host=db;Password=secret";
}

/// <summary>
/// A handler whose invocations a test can observe, with its behaviour chosen per job by the payload.
/// </summary>
internal sealed class ProbeJobHandler(
    ProbeInvocations invocations,
    [FromKeyedServices(QuartzRegistration.SchedulerName)] ISchedulerFactory schedulers,
    ILogger<ProbeJobHandler> logger)
    : BackgroundJobHandler<ProbePayload>(logger)
{
    protected override async Task<Result> ExecuteAsync(
        BackgroundJobContext job,
        ProbePayload payload,
        CancellationToken cancellationToken)
    {
        invocations.Record(job);
        if (payload.Behaviour == ProbeBehaviours.Fail)
        {
            return Result.Invalid(new ValidationError(ProbeBehaviours.FailureMessage));
        }

        await PlayAttemptAsync(job, payload.Behaviour, cancellationToken);
        return Result.Success();
    }

    // Throws, waits or does nothing, as the behaviour says for this attempt; an attempt that returns succeeds.
    private async Task PlayAttemptAsync(BackgroundJobContext job, string behaviour, CancellationToken cancellationToken)
    {
        switch (behaviour)
        {
            case ProbeBehaviours.Throw:
            case ProbeBehaviours.ThrowOnce when job.AttemptCount == 1:
            case ProbeBehaviours.BlockFirstThenThrow when job.AttemptCount > 1:
                throw new InvalidOperationException(ProbeBehaviours.SecretMessage);
            case ProbeBehaviours.BlockFirstThenThrowWhileStopping when job.AttemptCount == 2:
                await WaitForSchedulerToStopAsync(cancellationToken);
                throw new InvalidOperationException(ProbeBehaviours.SecretMessage);
            case ProbeBehaviours.FinishFirstAsNodeStops when job.AttemptCount == 1:
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                return;
            case ProbeBehaviours.Block:
            case ProbeBehaviours.BlockFirst when job.AttemptCount == 1:
            case ProbeBehaviours.BlockFirstThenThrow when job.AttemptCount == 1:
            case ProbeBehaviours.BlockFirstThenThrowWhileStopping when job.AttemptCount == 1:
                await BlockAsync(job, cancellationToken);
                return;
        }
    }

    // Puts the throw in the window where a stopping scheduler refuses new triggers but still completes the firings it
    // waits for.
    private async Task WaitForSchedulerToStopAsync(CancellationToken cancellationToken)
    {
        var scheduler = await schedulers.GetScheduler(cancellationToken);
        while (scheduler.Status is not (SchedulerStatus.ShuttingDown or SchedulerStatus.Shutdown))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
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
