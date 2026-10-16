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
    public FlattenedRowData(string dataJson, string searchJson)
    {
        DataJson = Guard.Against.NullOrEmpty(dataJson);
        SearchJson = Guard.Against.NullOrEmpty(searchJson);
    }

    public string DataJson { get; }

    public string SearchJson { get; }
}
