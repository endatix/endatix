using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Represents a submission data transfer object.
/// </summary>
/// <param name="Id">The unique identifier of the submission.</param>
/// <param name="IsComplete">Indicates if the submission is complete.</param>
/// <param name="JsonData">A dictionary containing JSON data related to the submission.</param>
/// <param name="FormDefinitionId">The unique identifier of the form definition associated with the submission.</param>
/// <param name="CurrentPage">The current page of the submission, if applicable.</param>
/// <param name="CompletedAt">The date and time when the submission was completed, if applicable.</param>
/// <param name="CreatedAt">The date and time when the submission was created.</param>
/// <param name="Metadata">Additional metadata related to the submission.</param>
public record SubmissionDto(long Id, bool IsComplete, Dictionary<string, object> JsonData, long FormDefinitionId, int? CurrentPage, DateTime? CompletedAt, DateTime CreatedAt, string? Metadata)
{
    public long Id { get; init; } = Id;
    public bool IsComplete { get; init; } = IsComplete;
    public Dictionary<string, object> JsonData { get; init; } = JsonData;
    public string? Metadata { get; init; } = Metadata;
    public long FormDefinitionId { get; init; } = FormDefinitionId;
    public int? CurrentPage { get; init; } = CurrentPage;
    public DateTime? CompletedAt { get; init; } = CompletedAt;
    public DateTime CreatedAt { get; init; } = CreatedAt;

    public static SubmissionDto FromSubmission(Submission submission)
    {
        Guard.Against.Null(submission, nameof(submission));

        try
        {
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(submission.JsonData) ?? [];

            return new SubmissionDto(
                Id: submission.Id,
                IsComplete: submission.IsComplete,
                JsonData: jsonData,
                FormDefinitionId: submission.FormDefinitionId,
                CurrentPage: submission.CurrentPage,
                CompletedAt: submission.CompletedAt,
                CreatedAt: submission.CreatedAt,
                Metadata: submission.Metadata
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize Submission's JSON data", ex);
        }
    }
}
