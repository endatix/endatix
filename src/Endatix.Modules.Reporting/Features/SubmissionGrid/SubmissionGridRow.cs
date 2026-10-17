using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// One submission on a submissions-grid page: its list fields, and its answers for the requested columns.
/// </summary>
/// <param name="Id">The submission id.</param>
/// <param name="FormId">The submission's form.</param>
/// <param name="FormDefinitionId">The form definition the submission was made with.</param>
/// <param name="IsComplete">Whether the submission is complete.</param>
/// <param name="CollectionStatus">The collection status code.</param>
/// <param name="Status">The submission status code.</param>
/// <param name="IsTestSubmission">Whether the submission is a test submission.</param>
/// <param name="CreatedAt">When the submission was created.</param>
/// <param name="ModifiedAt">When the submission was last changed.</param>
/// <param name="StartedAt">When the respondent started.</param>
/// <param name="CompletedAt">When the submission was completed.</param>
/// <param name="SubmitterId">The submitter's id, when known.</param>
/// <param name="SubmitterDisplayId">The submitter's display id, when known.</param>
/// <param name="SubmitterProfile">The submitter's profile fields, as they were when they submitted.</param>
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
    /// <summary>From the submission's prepared row.</summary>
    public const string Prepared = "prepared";

    /// <summary>From the submission's own answers, because its prepared row is not up to date.</summary>
    public const string Raw = "raw";
}
