using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// BI-ready submission row aligned to <see cref="FormExportSchema"/>.
/// Keyed by core <c>Submission.Id</c> — not a <see cref="Endatix.Core.Entities.BaseEntity"/> because the PK is external.
/// </summary>
public sealed class FlattenedSubmission : ITenantOwned, IAggregateRoot
{
    private FlattenedSubmission() { }

    public FlattenedSubmission(
        long submissionId,
        long tenantId,
        long formId,
        string dataJson)
    {
        Guard.Against.NegativeOrZero(submissionId, nameof(submissionId));
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NullOrEmpty(dataJson, nameof(dataJson));

        SubmissionId = submissionId;
        TenantId = tenantId;
        FormId = formId;
        DataJson = dataJson;
        CreatedAt = DateTime.UtcNow;
    }

    public long SubmissionId { get; private set; }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>
    /// Flat key-value answers aligned to <see cref="FormExportSchema.SchemaJson"/>.
    /// </summary>
    public string DataJson { get; private set; } = "{}";

    /// <summary>
    /// Mirrors core submission deletion for export filtering. Not full <see cref="Endatix.Core.Entities.BaseEntity"/> soft-delete.
    /// </summary>
    public bool IsDeleted { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    public void UpdateData(string dataJson)
    {
        Guard.Against.NullOrEmpty(dataJson, nameof(dataJson));

        DataJson = dataJson;
        IsDeleted = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        ModifiedAt = DateTime.UtcNow;
    }
}
