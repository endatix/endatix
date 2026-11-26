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
    /// <summary>Definition that governed this response, when needed to interpret a coded panel.</summary>
    public string? SubmittedDefinition { get; init; }

    /// <summary>Original response for reading the comment in the same panel item.</summary>
    public string? SubmittedAnswers { get; init; }

    /// <summary>
    /// Returns a row carrying the original submission answers from the exact revision and timestamp
    /// that produced <see cref="DataJson"/>. Omit this when the snapshots cannot be matched.
    /// Used only to disambiguate matrix cells during projection; not part of the serialized row.
    /// </summary>
    public FlattenedExportRow WithSourceAnswers(string answersJson) => this with { SourceAnswersJson = answersJson };

    internal string? SourceAnswersJson { get; init; }
}
