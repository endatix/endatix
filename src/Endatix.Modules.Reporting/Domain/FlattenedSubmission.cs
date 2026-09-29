using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// BI-ready submission row aligned to <see cref="Domain.FormSchema"/>.
/// </summary>
public sealed class FlattenedSubmission : ITenantOwned, IAggregateRoot
{
    private FlattenedSubmission() { }

    /// <summary>
    /// Creates a tracking row when flattening is queued or first attempted.
    /// </summary>
    public FlattenedSubmission(long submissionId, long tenantId, long formId)
    {
        Guard.Against.NegativeOrZero(submissionId);
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);

        SubmissionId = submissionId;
        TenantId = tenantId;
        FormId = formId;
        CreatedAt = DateTime.UtcNow;
        Integration = SubmissionIntegrationState.CreatePending(CreatedAt);
    }

    public long SubmissionId { get; private set; }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>
    /// Flat key-value answers aligned to <see cref="Domain.FormSchema.FlatteningMap"/>.
    /// Populated when <see cref="Integration"/> is processed.
    /// </summary>
    public string? DataJson { get; private set; }

    /// <summary>Reporting pipeline sync state (source of truth for integration/export readiness).</summary>
    public SubmissionIntegrationState Integration { get; private set; } = null!;

    /// <summary>
    /// Mirrors core submission deletion for export filtering.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// The <see cref="Endatix.Core.Entities.Submission.Revision"/> the row was last written from, or <c>null</c>
    /// before the first flatten recorded one. Writes from an older revision than this are refused, so flattens of
    /// one submission that finish out of order cannot put older data over newer.
    /// </summary>
    public long? SourceRevision { get; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    /// <summary>
    /// The source submission's own <c>ModifiedAt ?? CreatedAt</c> for the version in <see cref="DataJson"/>.
    /// Backfill compares it with the submission's current stamp to tell whether the row is stale:
    /// both values come from the same writer, unlike <see cref="ModifiedAt"/>, which the reporting
    /// worker sets after it has read the submission. Null until processed, and for rows processed
    /// before this was stored — those are reprocessed on the next backfill.
    /// </summary>
    public DateTime? SourceModifiedAt { get; private set; }

    public void MarkProcessing()
    {
        Integration.MarkProcessing();
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkProcessed(string dataJson, DateTime sourceModifiedAt)
    {
        Guard.Against.NullOrEmpty(dataJson);

        DataJson = dataJson;
        SourceModifiedAt = sourceModifiedAt;
        Integration.MarkProcessed();
        IsDeleted = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? error)
    {
        Integration.MarkFailed(error);
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkSkipped()
    {
        DataJson = null;
        SourceModifiedAt = null;
        Integration.MarkSkipped();
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        ModifiedAt = DateTime.UtcNow;
    }

    public SubmissionIntegrationSnapshotDto ToIntegrationSnapshot() => Integration.ToSnapshot();
}
