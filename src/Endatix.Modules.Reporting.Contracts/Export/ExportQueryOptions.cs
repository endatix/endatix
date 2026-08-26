namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Query options for streaming flattened submission export rows.
/// Date bounds are inclusive From / exclusive To (parsed UTC instants).
/// </summary>
public sealed record ExportQueryOptions(
    int PageSize = 500,
    long? AfterSubmissionId = null,
    bool IncludeTestSubmissions = false,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null,
    DateTime? StartedFrom = null,
    DateTime? StartedTo = null,
    DateTime? CompletedFrom = null,
    DateTime? CompletedTo = null,
    long? MinSubmissionId = null,
    long? MaxSubmissionId = null,
    bool? IsComplete = null);
