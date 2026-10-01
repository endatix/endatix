namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// One job to enqueue. Used both for a single <see cref="IBackgroundJobQueue.EnqueueAsync"/> and as
/// the element type of a fan-out batch.
/// </summary>
/// <remarks>
/// Feature code builds a request with <see cref="Create{TPayload}"/>, which takes the job type from the
/// payload and serializes it with the one shared serializer. The positional constructor is for generic
/// infrastructure that enqueues without knowing the payload type at compile time.
/// </remarks>
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
/// job apply the configured default for the job type.
/// </param>
/// <param name="DedupKey">
/// Optional identity of the unit of work, unique within a tenant and job type. Enqueueing the same key
/// again does not create a second job: it returns the id of the job that already exists, and schedules
/// nothing. It must name the work, not the attempt, so it never contains a timestamp or a counter.
/// </param>
public sealed record BackgroundJobRequest(
    string JobType,
    string PayloadJson,
    long TenantId,
    long? CreatedByUserId = null,
    DateTime? ExpiresAt = null,
    string? DedupKey = null)
{
    /// <summary>
    /// Builds a request for <paramref name="payload"/>, taking the job type from the payload type and the
    /// stored input from <see cref="BackgroundJobPayloadSerializer"/>.
    /// </summary>
    public static BackgroundJobRequest Create<TPayload>(
        TPayload payload,
        long tenantId,
        long? createdByUserId = null,
        DateTime? expiresAt = null,
        string? dedupKey = null)
        where TPayload : IBackgroundJobPayload
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new BackgroundJobRequest(
            TPayload.JobType,
            BackgroundJobPayloadSerializer.Serialize(payload),
            tenantId,
            createdByUserId,
            expiresAt,
            dedupKey);
    }
}
