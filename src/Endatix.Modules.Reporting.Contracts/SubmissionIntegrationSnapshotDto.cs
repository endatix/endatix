namespace Endatix.Modules.Reporting.Contracts;

/// <summary>
/// Read-model projection of reporting pipeline state for a submission (grid, detail, integration events).
/// </summary>
public sealed record SubmissionIntegrationSnapshotDto(
    string Status,
    DateTime? ProcessedAt,
    DateTime? LastAttemptAt,
    string? LastError);
