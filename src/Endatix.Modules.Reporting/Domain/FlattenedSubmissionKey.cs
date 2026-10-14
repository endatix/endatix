namespace Endatix.Modules.Reporting.Domain;

/// <summary>The submission a flattened row belongs to, by the ids that locate it.</summary>
public readonly record struct FlattenedSubmissionKey(long TenantId, long FormId, long SubmissionId);
