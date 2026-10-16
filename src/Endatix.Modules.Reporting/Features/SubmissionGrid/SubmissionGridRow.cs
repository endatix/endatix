using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// One submission on a submissions-grid page: its list fields, and its answers for the requested columns.
/// </summary>
/// <param name="RowState">One of <see cref="SubmissionGridRowStates"/>: how far the submission is prepared.</param>
/// <param name="Answers">The requested columns that have a value, keyed by column key.</param>
/// <param name="AnswersSource">
/// <see cref="SubmissionGridAnswerSources.Prepared"/> when <paramref name="Answers"/> comes from prepared data,
/// <see cref="SubmissionGridAnswerSources.Raw"/> when it comes from the submission's own answers.
/// </param>
public sealed record SubmissionGridRow(
    long Id,
    long FormId,
    long FormDefinitionId,
    bool IsComplete,
    string CollectionStatus,
    string Status,
    bool IsTestSubmission,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? SubmitterId,
    string? SubmitterDisplayId,
    IReadOnlyDictionary<string, string>? SubmitterProfile,
    string RowState,
    IReadOnlyDictionary<string, JsonElement> Answers,
    string AnswersSource);

/// <summary>
/// How far a submission is prepared for the grid.
/// </summary>
public static class SubmissionGridRowStates
{
    /// <summary>Prepared from the submission's current answers.</summary>
    public const string Prepared = "prepared";

    /// <summary>Prepared, but the submission changed since.</summary>
    public const string Stale = "stale";

    /// <summary>Queued or being prepared.</summary>
    public const string Pending = "pending";

    /// <summary>Preparing it failed.</summary>
    public const string Failed = "failed";

    /// <summary>Not prepared because it is in progress.</summary>
    public const string Skipped = "skipped";

    /// <summary>Never queued for preparation.</summary>
    public const string None = "none";
}

/// <summary>
/// Where a grid row's answers come from.
/// </summary>
public static class SubmissionGridAnswerSources
{
    public const string Prepared = "prepared";

    public const string Raw = "raw";
}
