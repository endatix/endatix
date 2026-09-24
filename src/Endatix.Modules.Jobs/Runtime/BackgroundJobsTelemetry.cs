using System.Diagnostics;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The tracing source job execution is reported on.
/// </summary>
internal static class BackgroundJobsTelemetry
{
    /// <summary>A tracing pipeline sees job execution only once it subscribes to this source.</summary>
    public const string ActivitySourceName = "Endatix.Jobs";

    private const string ExecuteActivityName = "background_job.execute";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    /// <summary>
    /// Starts the activity one attempt runs inside, continuing the trace stored on the row when it parses as W3C,
    /// so the enqueue and the execution read as one trace across the queue's asynchronous gap. A row whose trace is
    /// missing or unusable is traced on its own instead, because losing the link is not a reason to lose the span.
    /// Returns <see langword="null"/> when nothing listens to the source.
    /// </summary>
    public static Activity? StartExecution(ClaimedJob job)
    {
        var parent = job.TraceId is { Length: > 0 } traceId
            && ActivityContext.TryParse(traceId, traceState: null, isRemote: true, out var storedTrace)
            ? storedTrace
            : default;

        return _activitySource.StartActivity(
            ExecuteActivityName,
            ActivityKind.Consumer,
            parent,
            tags:
            [
                new("endatix.job.id", job.Id),
                new("endatix.job.type", job.JobType),
                new("endatix.job.attempt", job.AttemptCount),
            ]);
    }
}
