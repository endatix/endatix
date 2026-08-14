namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// One job to enqueue. Used both for a single <see cref="IBackgroundJobQueue.EnqueueAsync"/> and as
/// the element type of a fan-out batch.
/// </summary>
/// <param name="JobType">
/// Router key the handler registry resolves against, e.g. <c>SubmissionExport</c>. A string rather
/// than an enum so handlers can be contributed by assemblies this one does not reference.
/// </param>
/// <param name="PayloadJson">Handler input. Immutable once the job is created.</param>
/// <param name="TenantId">
/// Owning tenant. Carried on the row so a handler can scope its queries explicitly — the ambient
/// tenant filter is disabled in a background service, not enforced.
/// </param>
/// <param name="CreatedByUserId">
/// Requesting user, or <c>null</c> for system-enqueued work such as webhook fan-out.
/// </param>
/// <param name="ExpiresAt">
/// When the job row and any artifact it produced become collectable. <c>null</c> lets the retention
/// sweeper apply the configured default for the job type.
/// </param>
public sealed record BackgroundJobRequest(
    string JobType,
    string PayloadJson,
    long TenantId,
    long? CreatedByUserId = null,
    DateTime? ExpiresAt = null);
