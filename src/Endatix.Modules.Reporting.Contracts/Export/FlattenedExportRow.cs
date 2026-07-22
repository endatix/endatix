namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// BI-ready submission row streamed from the reporting read model.
/// </summary>
public sealed record FlattenedExportRow(
    long SubmissionId,
    long FormId,
    bool IsComplete,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? SubmitterId,
    string? SubmitterDisplayId,
    string DataJson);
