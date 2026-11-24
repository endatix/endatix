using Ardalis.GuardClauses;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// A write to a submission's flattened row, made from one revision of the submission. It lands only when the row
/// was not already written from a newer revision. <see cref="SourceModifiedAt"/> is the submission's own
/// <c>ModifiedAt ?? CreatedAt</c> as it was read, which a processed row keeps for the backfill's staleness check.
/// Construct a fresh value for each processing attempt, claim with it before computing, and pass that same value
/// to the terminal write. Reconstructing it from equal source fields does not carry the claim.
/// </summary>
public readonly record struct FlattenedRevision(long TenantId, long SubmissionId, long Revision, DateTime SourceModifiedAt)
{
    // Copies carry the claim to publication. Construct a new value for every attempt, including a retry.
    internal Guid AttemptId { get; } = Guid.NewGuid();
    internal bool KeepPrepared { get; init; }

    /// <summary>Compares source values, not ownership of a database claim.</summary>
    public bool Equals(FlattenedRevision other) => TenantId == other.TenantId && SubmissionId == other.SubmissionId &&
        Revision == other.Revision && SourceModifiedAt == other.SourceModifiedAt && KeepPrepared == other.KeepPrepared;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(TenantId, SubmissionId, Revision, SourceModifiedAt, KeepPrepared);
}

/// <summary>
/// The data a processed flatten stores: the flattened answers, and their search copy built from the schema's search
/// keys at <see cref="SearchKeysRevision"/>.
/// </summary>
public sealed record FlattenedRowData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlattenedRowData"/> class.
    /// </summary>
    /// <param name="dataJson">The flattened answers as JSON.</param>
    /// <param name="searchJson">The search copy as JSON.</param>
    /// <param name="searchKeysRevision">The form's search keys revision the search copy was built at.</param>
    public FlattenedRowData(string dataJson, string searchJson, int searchKeysRevision)
    {
        DataJson = Guard.Against.NullOrEmpty(dataJson);
        SearchJson = Guard.Against.NullOrEmpty(searchJson);
        SearchKeysRevision = Guard.Against.Negative(searchKeysRevision);
    }

    /// <summary>The flattened answers as JSON.</summary>
    public string DataJson { get; }

    /// <summary>The search copy as JSON: the scalar answers to the schema's search keys, as normalised text.</summary>
    public string SearchJson { get; }

    /// <summary>
    /// The form's search keys revision (<see cref="Features.SearchCopy.FormSearchKeys.Revision"/>) the search copy
    /// was built at: a key added at a later revision has no value here yet.
    /// </summary>
    public int SearchKeysRevision { get; }
}
