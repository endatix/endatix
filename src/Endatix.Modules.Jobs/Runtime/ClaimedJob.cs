using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// A job as a successful claim took it: what the claim returns to the runner.
/// </summary>
/// <remarks>
/// <see cref="AttemptCount"/> is the fence. It is the attempt the claim's own update consumed, and every
/// write the runner makes afterwards is conditional on it, which is why it travels with the job rather
/// than being counted locally or read back later. <see cref="Status"/> is the status the claim left the
/// row in, and <see cref="TraceId"/> comes along so the execution re-parents onto the request that
/// enqueued the job.
/// </remarks>
internal sealed record ClaimedJob(
    long Id,
    string JobType,
    long TenantId,
    string PayloadJson,
    int AttemptCount,
    string? TraceId,
    JobStatus Status);
