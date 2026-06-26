using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// BI-ready submission row aligned to <see cref="FormExportSchema"/>.
/// Keyed by core <c>Submission.Id</c> — not a <see cref="BaseEntity"/> because the PK is external.
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
        Guard.Against.NullOrEmpty(dataJson);

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
    /// Flat key-value answers aligned to <see cref="FormExportSchema.SchemaJson"/>.
    /// Populated when <see cref="Integration"/> is processed.
    /// </summary>
    public string? DataJson { get; private set; }

    /// <summary>Reporting pipeline sync state (source of truth for integration/export readiness).</summary>
    public SubmissionIntegrationState Integration { get; private set; } = null!;

    /// <summary>
    /// Mirrors core submission deletion for export filtering.
    /// </summary>
    public bool IsDeleted { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    public void MarkProcessing()
    {
        Integration.MarkProcessing();
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkProcessed(string dataJson)
    {
        Guard.Against.NullOrEmpty(dataJson, nameof(dataJson));

        DataJson = dataJson;
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
