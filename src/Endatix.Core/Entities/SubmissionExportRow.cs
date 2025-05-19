namespace Endatix.Core.Entities;

public class SubmissionExportRow
{
    public long FormId { get; set; }
    public long Id { get; set; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string AnswersModel { get; set; } = string.Empty;
} 