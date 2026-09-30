using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Jobs.Domain;

/// <summary>
/// One durable unit of background work. The row <em>is</em> the queue entry and the single source of
/// truth for the job's state — there is no second system holding a competing status, attempt count,
/// or lease to reconcile against.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="ITenantOwned"/> deliberately, unlike <see cref="OutboxMessage"/>. Every job
/// row belongs to a real tenant, so the global filter gives tenant-scoped reads for free on the
/// request path — and because the ambient tenant resolves to 0 outside a request, the filter is
/// permissive there, which is what lets the job wrapper read any tenant's row by id with no special
/// casing. Do <b>not</b> "fix" this by giving background services a tenant scope: the wrapper would
/// no longer find the jobs it is asked to run.
/// </para>
/// <para>
/// The transition methods below are the readable, guarded expression of the state machine and are
/// what the tests exercise. The job wrapper's writes — claim, outcome, progress — deliberately bypass
/// them with <c>ExecuteUpdateAsync</c>, because a tracked entity write could not be fenced on the
/// claimed attempt and would drag <see cref="BaseEntity.ModifiedAt"/> along with it. Both paths must
/// agree on the rules, which is why the rules live here.
/// </para>
/// <para>
/// The row is the job's record, not its schedule: the scheduler decides when a job runs, and this row
/// says what state it is in.
/// </para>
/// </remarks>
public class BackgroundJob : BaseEntity, IAggregateRoot, ITenantOwned
{
    private BackgroundJob() { } // For EF Core

    /// <summary>
    /// Creates a job that is eligible to run at <paramref name="nextAttemptAt"/>.
    /// </summary>
    public BackgroundJob(
        string jobType,
        string payloadJson,
        long tenantId,
        DateTime nextAttemptAt,
        long? createdByUserId = null,
        DateTime? expiresAt = null,
        string? traceId = null)
    {
        Guard.Against.NullOrWhiteSpace(jobType);
        Guard.Against.NullOrWhiteSpace(payloadJson);
        // Every job belongs to a tenant. Zero is the value the tenant context reports when there is
        // no ambient tenant at all, so a row carrying it belongs to nobody rather than to everybody
        // — and the tenant filter would hide it from every tenant while showing it to every
        // background service. Platform-wide work needs its own design, not this sentinel.
        Guard.Against.NegativeOrZero(tenantId);

        JobType = jobType;
        PayloadJson = payloadJson;
        TenantId = tenantId;
        NextAttemptAt = nextAttemptAt;
        CreatedByUserId = createdByUserId;
        ExpiresAt = expiresAt;
        TraceId = traceId;
        Status = JobStatus.Pending;
        AttemptCount = 0;
    }

    /// <summary>
    /// Creates the job <paramref name="request"/> asks for, eligible to run at <paramref name="nextAttemptAt"/>,
    /// under the dedup key the request carries.
    /// </summary>
    public static BackgroundJob FromRequest(BackgroundJobRequest request, DateTime nextAttemptAt, string? traceId)
    {
        Guard.Against.Null(request);
        if (DedupKeyError(request.DedupKey) is { } error)
        {
            throw new ArgumentException(error, nameof(request));
        }

        return new BackgroundJob(
            request.JobType,
            request.PayloadJson,
            request.TenantId,
            nextAttemptAt,
            request.CreatedByUserId,
            request.ExpiresAt,
            traceId)
        {
            DedupKey = string.IsNullOrWhiteSpace(request.DedupKey) ? null : request.DedupKey,
        };
    }

    // Refused here, naming the limit, rather than by the database as a truncation error that fails every other job
    // in the same batch without saying which key was too long.
    private static string? DedupKeyError(string? dedupKey) =>
        dedupKey is { Length: > DedupKeyMaxLength }
            ? $"A dedup key is at most {DedupKeyMaxLength} characters; this one has {dedupKey.Length}."
            : null;

    /// <summary>Router key the handler registry resolves against, e.g. <c>SubmissionExport</c>.</summary>
    public string JobType { get; private set; } = null!;

    /// <summary>Owning tenant. Handlers must scope their queries to this explicitly.</summary>
    public long TenantId { get; private set; }

    public JobStatus Status { get; private set; }

    /// <summary>Handler input, immutable after creation.</summary>
    public string PayloadJson { get; private set; } = null!;

    /// <summary>0–100, for display only. Never load-bearing for crash detection.</summary>
    public int ProgressPercentage { get; private set; }

    /// <summary>Human-readable phase, e.g. "Streaming row 4,500 of 10,000".</summary>
    public string? StatusMessage { get; private set; }

    /// <summary>
    /// Why the most recent attempt failed. Set on <see cref="JobStatus.Failed"/> and
    /// <see cref="JobStatus.DeadLettered"/>, and also on <see cref="JobStatus.Retrying"/>, where it
    /// describes the attempt just ended rather than the job.
    /// </summary>
    /// <remarks>
    /// Must be author-written text that is safe to show an end user: the job status endpoint returns
    /// this column verbatim, so an exception message assigned here reaches the caller.
    /// </remarks>
    public string? ErrorMessage { get; private set; }

    /// <summary>Requesting user; null for system-enqueued work such as webhook fan-out.</summary>
    public long? CreatedByUserId { get; private set; }

    /// <summary>When the first attempt was claimed.</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>When the job reached a terminal status.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// When the row and any artifact it produced become collectable by the retention job. Past
    /// this point a download returns <c>410</c>.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Attempts <em>started</em>, incremented once per <see cref="Claim"/>. Counting at claim rather
    /// than at failure keeps one rule for every way an attempt can end — success, deterministic
    /// failure, throw, or a worker that died without reporting anything — so a job that crashes its
    /// worker cannot retry forever.
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// When the next attempt is due, mirrored from the scheduler so the status endpoint can show it. Set
    /// to now at enqueue and to the retry time after a retryable failure. Nothing polls this column: the
    /// scheduler decides when the job actually runs.
    /// </summary>
    public DateTime NextAttemptAt { get; private set; }

    /// <summary>
    /// W3C trace id captured at enqueue, so execution re-parents onto the request that caused it and
    /// the async gap renders as one distributed trace.
    /// </summary>
    public string? TraceId { get; private set; }

    /// <summary>
    /// The caller's identity for the unit of work, unique within a tenant and job type, or <c>null</c> when
    /// the caller does not deduplicate. A second enqueue of the same key returns this job instead of creating
    /// another.
    /// </summary>
    public string? DedupKey { get; private set; }

    /// <summary>The longest dedup key a job row holds: an outbox message id and a subscriber key fit well inside.</summary>
    public const int DedupKeyMaxLength = 200;

    /// <summary>Whether this job has reached a state it can never leave.</summary>
    public bool IsTerminal => Status is JobStatus.Completed
        or JobStatus.Failed
        or JobStatus.DeadLettered
        or JobStatus.Canceled;

    /// <summary>
    /// Takes ownership of the job for execution and consumes an attempt.
    /// </summary>
    public void Claim(DateTime utcNow)
    {
        EnsureStatus(nameof(Claim), JobStatus.Pending, JobStatus.Retrying);

        Status = JobStatus.Processing;
        AttemptCount++;
        StartedAt ??= utcNow;
    }

    /// <summary>Records user-facing progress. Not a liveness signal: the scheduler detects dead nodes.</summary>
    public void ReportProgress(int progressPercentage, string? statusMessage = null)
    {
        EnsureStatus(nameof(ReportProgress), JobStatus.Processing);
        Guard.Against.OutOfRange(progressPercentage, nameof(progressPercentage), 0, 100);

        ProgressPercentage = progressPercentage;
        StatusMessage = statusMessage ?? StatusMessage;
    }

    /// <summary>Completes the job successfully.</summary>
    public void Complete(DateTime utcNow)
    {
        EnsureStatus(nameof(Complete), JobStatus.Processing);

        Status = JobStatus.Completed;
        ProgressPercentage = 100;
        CompletedAt = utcNow;
        // An earlier attempt may have recorded one; a job that ended in success must not still
        // display the failure that preceded it.
        ErrorMessage = null;
    }

    /// <summary>
    /// Ends the job on a <b>deterministic</b> failure — one that retrying cannot fix. Terminal, and
    /// distinct from <see cref="DeadLetter"/>, which means the opposite: retryable, but out of budget.
    /// </summary>
    public void Fail(string errorMessage, DateTime utcNow)
    {
        EnsureStatus(nameof(Fail), JobStatus.Processing);
        Guard.Against.NullOrWhiteSpace(errorMessage);

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = utcNow;
    }

    /// <summary>
    /// Schedules another attempt after a retryable failure. Does not consume an attempt — the attempt
    /// was consumed by the <see cref="Claim"/> that started it.
    /// </summary>
    public void Reschedule(DateTime nextAttemptAt, string? errorMessage = null)
    {
        EnsureStatus(nameof(Reschedule), JobStatus.Processing);

        Status = JobStatus.Retrying;
        NextAttemptAt = nextAttemptAt;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Ends the job after a retryable failure exhausted its attempt budget.
    /// </summary>
    public void DeadLetter(string errorMessage, DateTime utcNow)
    {
        EnsureStatus(nameof(DeadLetter), JobStatus.Processing);
        Guard.Against.NullOrWhiteSpace(errorMessage);

        Status = JobStatus.DeadLettered;
        ErrorMessage = errorMessage;
        CompletedAt = utcNow;
    }

    /// <summary>
    /// Cancels the job. From <see cref="JobStatus.Pending"/> or <see cref="JobStatus.Retrying"/> this
    /// is the whole operation, because a claim will refuse it. From
    /// <see cref="JobStatus.Processing"/> the job wrapper's cancellation watcher observes the status and
    /// trips the handler's cancellation token.
    /// </summary>
    public void Cancel(DateTime utcNow)
    {
        EnsureStatus(nameof(Cancel), JobStatus.Pending, JobStatus.Retrying, JobStatus.Processing);

        Status = JobStatus.Canceled;
        CompletedAt = utcNow;
    }

    /// <summary>Sets the retention deadline for this job and any artifact it produced.</summary>
    public void SetExpiry(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    // Terminal statuses are immutable, and the non-terminal ones each admit only specific successors.
    // Guarding here means an out-of-order call is a loud failure in a test rather than a silently
    // corrupt row that a later write reasons about incorrectly.
    private void EnsureStatus(string operation, params JobStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Cannot {operation} a background job in status {Status}; expected one of {string.Join(", ", allowed)}.");
        }
    }
}
