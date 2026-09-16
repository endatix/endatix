using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// A job row as the runner reads it back immediately after claiming it.
/// </summary>
/// <remarks>
/// A set-based claim reports only how many rows it changed, so the runner reads the row to learn which
/// attempt it took. That value fences every write the runner makes afterwards, which is why it travels
/// with the job rather than being counted locally. <see cref="Status"/> comes along so a runner that
/// lost the row between the claim and this read can see that it did, and <see cref="TraceId"/> so the
/// execution re-parents onto the request that enqueued the job.
/// </remarks>
internal sealed record ClaimedJob(
    long Id,
    string JobType,
    long TenantId,
    string PayloadJson,
    int AttemptCount,
    string? TraceId,
    JobStatus Status);
