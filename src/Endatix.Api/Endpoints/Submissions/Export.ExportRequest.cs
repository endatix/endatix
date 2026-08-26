using Endatix.Api.Common;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Api.Endpoints.Submissions;

public class ExportRequest : ICreatedRange, IModifiedRange, IStartedRange, ICompletedRange
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

    /// <inheritdoc />
    public string? CreatedFrom { get; set; }

    /// <inheritdoc />
    public string? CreatedTo { get; set; }

    /// <inheritdoc />
    public string? ModifiedFrom { get; set; }

    /// <inheritdoc />
    public string? ModifiedTo { get; set; }

    /// <inheritdoc />
    public string? StartedFrom { get; set; }

    /// <inheritdoc />
    public string? StartedTo { get; set; }

    /// <inheritdoc />
    public string? CompletedFrom { get; set; }

    /// <inheritdoc />
    public string? CompletedTo { get; set; }

    public long? MinSubmissionId { get; set; }

    public long? MaxSubmissionId { get; set; }
}
