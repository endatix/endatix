using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportRequest
{
    public long FormId { get; set; }

    public string? ExportFormat { get; set; }

    public long? ExportId { get; set; }

    public long? ExportFormatId { get; set; }

    public bool? IncludeTestSubmissions { get; set; }

    public string[]? ColumnScope { get; set; }

    /// <summary>
    /// Optional codebook label locale for this export run. Overrides format settings.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Optional completion filter. Omitted means all completion states.
    /// Wire: <c>all</c> | <c>completed</c> | <c>incomplete</c>.
    /// </summary>
    public ExportCompletionStatus? CompletionStatus { get; set; }

    public DateTime? CreatedAfter { get; set; }

    public DateTime? CreatedBefore { get; set; }

    public DateTime? StartedAfter { get; set; }

    public DateTime? StartedBefore { get; set; }

    public DateTime? CompletedAfter { get; set; }

    public DateTime? CompletedBefore { get; set; }

    public long? MinSubmissionId { get; set; }

    public long? MaxSubmissionId { get; set; }
}