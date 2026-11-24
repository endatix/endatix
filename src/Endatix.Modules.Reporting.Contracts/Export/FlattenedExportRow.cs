namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// BI-ready submission row streamed from the reporting read model.
/// </summary>
public sealed record FlattenedExportRow(
    long SubmissionId,
    long FormId,
    bool IsComplete,
    string CollectionStatus,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? SubmitterId,
    string? SubmitterDisplayId,
    string DataJson)
{
    // A flattened matrix cell can be either a numeric source code or an already-encoded string choice.
    internal string? SourceAnswersJson { get; init; }
}
