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
    /// Creates a tracking row for the submission <paramref name="key"/> locates, when flattening is queued or first
    /// attempted at <paramref name="createdAt"/>.
    /// </summary>
    public FlattenedSubmission(FlattenedSubmissionKey key, DateTime createdAt)
    {
        Guard.Against.NegativeOrZero(key.SubmissionId);
        Guard.Against.NegativeOrZero(key.TenantId);
        Guard.Against.NegativeOrZero(key.FormId);
        Guard.Against.Default(createdAt);
        Guard.Against.Expression(dateTime => dateTime.Kind != DateTimeKind.Utc, createdAt, "createdAt must be in UTC");

        SubmissionId = key.SubmissionId;
        TenantId = key.TenantId;
        FormId = key.FormId;
        CreatedAt = createdAt;
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

    /// <summary>
    /// The search copy of <see cref="DataJson"/>: the answered search keys, text lowercased and dates normalised, so
    /// answer filters read a small document instead of every answer. Written with <see cref="DataJson"/>, and null
    /// whenever it is. It holds the keys the schema had when the row was last written; a key dropped since stays
    /// until the row is flattened or rebuilt again, and filters read only the schema's current keys.
    /// </summary>
    public string? SearchJson { get; }

    /// <summary>
    /// The <see cref="Domain.FormSchema.SearchKeysRevision"/> of the search keys <see cref="SearchJson"/> was written
    /// from; 0 before any. A key added at a later revision has no value in this row until the row is rebuilt, so
    /// filters on that key count the row as not checked. Written by the repository's conditional writes and by the
    /// search copy rebuild, never by the entity itself.
    /// </summary>
    public int SearchKeysRevision { get; }

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

    /// <summary>
    /// Written by the repository's conditional writes and by the context when it saves a tracked change, never by
    /// the entity itself.
    /// </summary>
    public DateTime? ModifiedAt { get; }

    /// <summary>
    /// The source submission's own <c>ModifiedAt ?? CreatedAt</c> for the version in <see cref="DataJson"/>.
    /// Backfill compares it with the submission's current stamp to tell whether the row is stale:
    /// both values come from the same writer, unlike <see cref="ModifiedAt"/>, which the reporting
    /// worker sets after it has read the submission. Null until processed, and for rows processed
    /// before this was stored — those are reprocessed on the next backfill.
    /// </summary>
    public DateTime? SourceModifiedAt { get; private set; }

    public void MarkDeleted()
    {
        IsDeleted = true;
    }

    public SubmissionIntegrationSnapshotDto ToIntegrationSnapshot() => Integration.ToSnapshot();
}
