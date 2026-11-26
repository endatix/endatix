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
    /// <summary>
    /// Returns a row carrying the original submission answers from the exact revision and timestamp
    /// that produced <see cref="DataJson"/>. Omit this when the snapshots cannot be matched.
    /// Used only to disambiguate matrix cells during projection; not part of the serialized row.
    /// </summary>
    public FlattenedExportRow WithSourceAnswers(string answersJson) => this with { SourceAnswersJson = answersJson };

    internal string? SourceAnswersJson { get; init; }

    /// <summary>
    /// Definition that governed this response, read with <see cref="SourceAnswersJson"/> to classify a
    /// coded panel's Other text. Set only inside the reporting module; not part of the serialized row.
    /// </summary>
    internal string? SubmittedDefinition { get; init; }
}
