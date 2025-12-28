namespace Endatix.Core.Entities;

public class SubmissionExportRow
{
    public long FormId { get; init; }
    public long Id { get; init; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string AnswersModel { get; init; } = string.Empty;
} 