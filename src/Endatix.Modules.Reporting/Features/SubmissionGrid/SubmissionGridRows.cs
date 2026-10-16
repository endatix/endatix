using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// A submission's prepared row, as far as the grid needs it.
/// </summary>
/// <param name="HasData">Whether the row holds prepared answers, read even when <paramref name="DataJson"/> is not.</param>
internal sealed record PreparedRow(
    long SubmissionId,
    string StatusCode,
    bool HasData,
    string? DataJson,
    DateTime? SourceModifiedAt);

/// <summary>
/// Builds grid rows from a page of submissions and their prepared rows.
/// </summary>
internal static class SubmissionGridRows
{
    internal static SubmissionGridRow Compose(
        SubmissionDto submission,
        PreparedRow? prepared,
        IReadOnlyList<string> columns)
    {
        var rowState = RowStateOf(submission, prepared);
        var usePrepared = rowState is SubmissionGridRowStates.Prepared;
        var answers = PickAnswers(usePrepared ? prepared!.DataJson : submission.JsonData, columns);

        return new SubmissionGridRow(
            submission.Id,
            submission.FormId,
            submission.FormDefinitionId,
            submission.IsComplete,
            submission.CollectionStatus,
            submission.Status,
            submission.IsTestSubmission,
            submission.CreatedAt,
            submission.ModifiedAt,
            submission.StartedAt,
            submission.CompletedAt,
            submission.SubmitterId,
            submission.SubmitterDisplayId,
            submission.SubmitterProfile,
            rowState,
            answers,
            usePrepared ? SubmissionGridAnswerSources.Prepared : SubmissionGridAnswerSources.Raw);
    }

    /// <summary>
    /// The row's preparation state. A prepared row is current only when it was written from the submission's
    /// current version: both stamps come from the submission (see <c>FlattenedSubmission.SourceModifiedAt</c>).
    /// </summary>
    internal static string RowStateOf(SubmissionDto submission, PreparedRow? prepared)
    {
        if (prepared is null)
        {
            return SubmissionGridRowStates.None;
        }

        return prepared.StatusCode switch
        {
            SubmissionIntegrationStatusCodes.Processed when IsCurrent(submission, prepared) => SubmissionGridRowStates.Prepared,
            SubmissionIntegrationStatusCodes.Processed => SubmissionGridRowStates.Stale,
            SubmissionIntegrationStatusCodes.Failed => SubmissionGridRowStates.Failed,
            SubmissionIntegrationStatusCodes.Skipped => SubmissionGridRowStates.Skipped,
            _ => SubmissionGridRowStates.Pending,
        };
    }

    /// <summary>
    /// The values of <paramref name="columns"/> in a flat answer object. Keys that are missing or null are left
    /// out; answers that are not a JSON object give none.
    /// </summary>
    internal static IReadOnlyDictionary<string, JsonElement> PickAnswers(string? answersJson, IReadOnlyList<string> columns)
    {
        Dictionary<string, JsonElement> answers = new(StringComparer.Ordinal);
        if (columns.Count == 0 || string.IsNullOrWhiteSpace(answersJson) || !TryParseObject(answersJson, out var root))
        {
            return answers;
        }

        foreach (var key in columns)
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind is not JsonValueKind.Null)
            {
                answers[key] = value.Clone();
            }
        }

        return answers;
    }

    private static bool IsCurrent(SubmissionDto submission, PreparedRow prepared) =>
        prepared.HasData &&
        prepared.SourceModifiedAt == (submission.ModifiedAt ?? submission.CreatedAt);

    private static bool TryParseObject(string json, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
