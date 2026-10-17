using Ardalis.GuardClauses;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// A write to a submission's flattened row, made from one revision of the submission. It lands only when the row
/// was not already written from a newer revision. <see cref="SourceModifiedAt"/> is the submission's own
/// <c>ModifiedAt ?? CreatedAt</c> as it was read, which a processed row keeps for the backfill's staleness check.
/// </summary>
public readonly record struct FlattenedRevision(long TenantId, long SubmissionId, long Revision, DateTime SourceModifiedAt);

/// <summary>
/// The data a processed flatten stores: the flattened answers, and their search copy built from the schema's search
/// keys.
/// </summary>
public sealed record FlattenedRowData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlattenedRowData"/> class.
    /// </summary>
    /// <param name="dataJson">The flattened answers as JSON.</param>
    /// <param name="searchJson">The search copy as JSON.</param>
    public FlattenedRowData(string dataJson, string searchJson)
    {
        DataJson = Guard.Against.NullOrEmpty(dataJson);
        SearchJson = Guard.Against.NullOrEmpty(searchJson);
    }

    /// <summary>The flattened answers as JSON.</summary>
    public string DataJson { get; }

    /// <summary>The search copy as JSON: the scalar answers to the schema's search keys, as normalised text.</summary>
    public string SearchJson { get; }
}
