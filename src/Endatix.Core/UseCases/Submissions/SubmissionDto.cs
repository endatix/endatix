using Ardalis.GuardClauses;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Represents a submission data transfer object.
/// </summary>
/// <param name="Id">The unique identifier of the submission.</param>
/// <param name="IsComplete">Indicates if the submission is complete.</param>
/// <param name="JsonData">The JSON data related to the submission.</param>
/// <param name="FormId">The unique identifier of the form associated with the submission.</param>
/// <param name="FormDefinitionId">The unique identifier of the form definition associated with the submission.</param>
/// <param name="CurrentPage">The current page of the submission, if applicable.</param>
/// <param name="CompletedAt">The date and time when the submission was completed, if applicable.</param>
/// <param name="CreatedAt">The date and time when the submission was created.</param>
/// <param name="Metadata">Additional metadata related to the submission.</param>
/// <param name="Status">The status of the submission.</param>
/// <param name="SubmittedBy">The unique identifier of the user who created the submission, if applicable.</param>
public record SubmissionDto(long Id, bool IsComplete, string JsonData, long FormId, long FormDefinitionId, int? CurrentPage, DateTime? CompletedAt, DateTime CreatedAt, string? Metadata, string Status, string? SubmittedBy)
{
    public long Id { get; init; } = Id;
    public bool IsComplete { get; init; } = IsComplete;
    public string JsonData { get; init; } = JsonData;
    public string? Metadata { get; init; } = Metadata;
    public long FormId { get; init; } = FormId;
    public long FormDefinitionId { get; init; } = FormDefinitionId;
    public int? CurrentPage { get; init; } = CurrentPage;
    public DateTime? CompletedAt { get; init; } = CompletedAt;
    public DateTime CreatedAt { get; init; } = CreatedAt;
    public string Status { get; init; } = Status;
    public string? SubmittedBy { get; init; } = SubmittedBy;

    public static SubmissionDto FromSubmission(Submission submission)
    {
        Guard.Against.Null(submission, nameof(submission));

        return new SubmissionDto(
            Id: submission.Id,
            IsComplete: submission.IsComplete,
            JsonData: submission.JsonData,
            FormId: submission.FormId,
            FormDefinitionId: submission.FormDefinitionId,
            CurrentPage: submission.CurrentPage,
            CompletedAt: submission.CompletedAt,
            CreatedAt: submission.CreatedAt,
            Metadata: submission.Metadata,
            Status: submission.Status.Code,
            SubmittedBy: submission.SubmittedBy
        );
    }
}
