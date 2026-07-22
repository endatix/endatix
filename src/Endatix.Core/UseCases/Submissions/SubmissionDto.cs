using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using System.Text.Json;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Represents a submission data transfer object.
/// </summary>
public record SubmissionDto(
    long Id,
    bool IsComplete,
    string JsonData,
    long FormId,
    long FormDefinitionId,
    int? CurrentPage,
    DateTime? CompletedAt,
    DateTime? StartedAt,
    DateTime CreatedAt,
    string? Metadata,
    string Status,
    string? SubmittedBy,
    long? SubmitterId,
    string? SubmitterDisplayId,
    string? SubmitterProfileSnapshot,
    bool IsTestSubmission)
{
    public long Id { get; init; } = Id;
    public bool IsComplete { get; init; } = IsComplete;
    public string JsonData { get; init; } = JsonData;
    public string? Metadata { get; init; } = Metadata;
    public long FormId { get; init; } = FormId;
    public long FormDefinitionId { get; init; } = FormDefinitionId;
    public int? CurrentPage { get; init; } = CurrentPage;
    public DateTime? CompletedAt { get; init; } = CompletedAt;
    public DateTime? StartedAt { get; init; } = StartedAt;
    public DateTime CreatedAt { get; init; } = CreatedAt;
    public string Status { get; init; } = Status;
    public string? SubmittedBy { get; init; } = SubmittedBy;
    public long? SubmitterId { get; init; } = SubmitterId;
    public string? SubmitterDisplayId { get; init; } = SubmitterDisplayId;
    public IReadOnlyDictionary<string, string>? SubmitterProfile { get; init; } = ParseSubmitterProfile(SubmitterProfileSnapshot);
    public bool IsTestSubmission { get; init; } = IsTestSubmission;

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
            StartedAt: submission.StartedAt,
            CreatedAt: submission.CreatedAt,
            Metadata: submission.Metadata,
            Status: submission.Status.Code,
            SubmittedBy: submission.SubmittedBy,
            SubmitterId: submission.SubmitterId,
            SubmitterDisplayId: submission.SubmitterDisplayId,
            SubmitterProfileSnapshot: submission.SubmitterProfileSnapshot,
            IsTestSubmission: submission.IsTestSubmission);
    }

    private static IReadOnlyDictionary<string, string>? ParseSubmitterProfile(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot);
    }
}
