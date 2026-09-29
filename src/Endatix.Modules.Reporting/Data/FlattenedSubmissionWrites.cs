namespace Endatix.Modules.Reporting.Data;

/// <summary>The submission a flattened row belongs to, by the ids that locate it.</summary>
public readonly record struct FlattenedSubmissionKey(long TenantId, long FormId, long SubmissionId);

/// <summary>
/// A write to a submission's flattened row, made from one revision of the submission. It lands only when the row
/// was not already written from a newer revision.
/// </summary>
public readonly record struct FlattenedRevision(long TenantId, long SubmissionId, long Revision);
