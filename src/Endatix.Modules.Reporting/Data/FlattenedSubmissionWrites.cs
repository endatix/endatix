namespace Endatix.Modules.Reporting.Data;

/// <summary>The submission a flattened row belongs to, by the ids that locate it.</summary>
public readonly record struct FlattenedSubmissionKey(long TenantId, long FormId, long SubmissionId);

/// <summary>
/// A write to a submission's flattened row, made from one revision of the submission. It lands only when the row
/// was not already written from a newer revision. <see cref="SourceModifiedAt"/> is the submission's own
/// <c>ModifiedAt ?? CreatedAt</c> as it was read, which a processed row keeps for the backfill's staleness check.
/// </summary>
public readonly record struct FlattenedRevision(long TenantId, long SubmissionId, long Revision, DateTime SourceModifiedAt);
