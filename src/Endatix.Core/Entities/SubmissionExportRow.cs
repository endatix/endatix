using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a row of submission data for export operations.
/// </summary>
public class SubmissionExportRow : IExportItem
{
    public long FormId { get; init; }
    public long Id { get; init; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string AnswersModel { get; init; } = string.Empty;
} 