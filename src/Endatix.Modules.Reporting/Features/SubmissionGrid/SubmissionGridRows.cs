using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

internal sealed record PreparedRow(
    long SubmissionId,
    string StatusCode,
    bool HasData,
    string? DataJson,
    DateTime? SourceModifiedAt);

internal static class SubmissionGridRows
{
    internal static SubmissionGridRow Compose(
        SubmissionDto submission,
        PreparedRow? prepared,
        IReadOnlyList<string> columns)
    {
        var rowState = RowStateOf(submission, prepared);
        var usePrepared = rowState is SubmissionGridRowStates.Prepared;
        var answers = PickAnswers(usePrepared ? prepared?.DataJson : submission.JsonData, columns);

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

    internal static string RowStateOf(SubmissionDto submission, PreparedRow? prepared)
    {
        if (prepared is null)
        {
            return SubmissionGridRowStates.None;
        }

        return prepared.StatusCode switch
        {
            SubmissionIntegrationStatusCodes.Processed when IsPreparedFromCurrentVersion(submission, prepared) =>
                SubmissionGridRowStates.Prepared,
            SubmissionIntegrationStatusCodes.Processed => SubmissionGridRowStates.Stale,
            SubmissionIntegrationStatusCodes.Failed => SubmissionGridRowStates.Failed,
            SubmissionIntegrationStatusCodes.Skipped => SubmissionGridRowStates.Skipped,
            _ => SubmissionGridRowStates.Pending,
        };
    }

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

    private static bool IsPreparedFromCurrentVersion(SubmissionDto submission, PreparedRow prepared) =>
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
