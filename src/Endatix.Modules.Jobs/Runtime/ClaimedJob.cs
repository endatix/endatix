using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// A job as a successful claim took it. <see cref="AttemptCount"/> is the attempt that claim consumed,
/// and every later write for the job is fenced on it.
/// </summary>
internal sealed record ClaimedJob(
    long Id,
    string JobType,
    long TenantId,
    string PayloadJson,
    int AttemptCount,
    string? TraceId,
    JobStatus Status);
